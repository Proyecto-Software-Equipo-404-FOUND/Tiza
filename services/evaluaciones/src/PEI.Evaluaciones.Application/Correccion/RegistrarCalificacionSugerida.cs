using MediatR;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Correccion;

// S2-EV-02 (#144): IA registra la nota sugerida de una pregunta abierta (callback de la tool MCP).
// Contrato: POST /interno/entregas/{entregaId}/calificacion-sugerida (evaluaciones-v1.json).
public sealed record RegistrarCalificacionSugeridaCommand(
    Guid EntregaId,
    Guid PreguntaId,
    decimal NotaSugerida,
    string? Justificacion,
    string? DesgloseJson) : IRequest<ResultadoOperacion<CalificacionSugeridaRegistrada>>;

public sealed record CalificacionSugeridaRegistrada(
    Guid EntregaId,
    Guid PreguntaId,
    decimal NotaSugerida,
    bool YaExistia,
    string EstadoEntrega);

public sealed class RegistrarCalificacionSugeridaHandler
    : IRequestHandler<RegistrarCalificacionSugeridaCommand, ResultadoOperacion<CalificacionSugeridaRegistrada>>
{
    private readonly ICorreccionRepositorio _repositorio;

    public RegistrarCalificacionSugeridaHandler(ICorreccionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<CalificacionSugeridaRegistrada>> Handle(
        RegistrarCalificacionSugeridaCommand request, CancellationToken ct)
    {
        // 1. Datos básicos (400)
        if (request.PreguntaId == Guid.Empty)
            return Invalido("preguntaId es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Justificacion))
            return Invalido("La justificación no puede estar vacía.");
        if (request.Justificacion.Length > 4000)
            return Invalido("La justificación no puede superar los 4000 caracteres.");
        if (request.NotaSugerida < 0)
            return Invalido("La nota sugerida no puede ser negativa.");

        // 2. Entrega y pregunta (404)
        var entrega = await _repositorio.ObtenerEntregaAsync(request.EntregaId, ct);
        if (entrega is null)
            return ResultadoOperacion<CalificacionSugeridaRegistrada>.NoEncontrado();

        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(entrega.EvaluacionId, entrega.InstitucionId, ct);
        if (evaluacion is null)
            return ResultadoOperacion<CalificacionSugeridaRegistrada>.NoEncontrado();

        var abiertas = PreguntasCorreccion.LeerAbiertas(evaluacion.PreguntasJson);
        if (!abiertas.TryGetValue(request.PreguntaId, out var pregunta))
        {
            // Existe pero no es abierta (400) o no existe en la evaluación (404).
            return evaluacion.PreguntasJson.Contains(request.PreguntaId.ToString(), StringComparison.OrdinalIgnoreCase)
                ? Invalido("La pregunta no es de tipo Abierta.")
                : ResultadoOperacion<CalificacionSugeridaRegistrada>.NoEncontrado();
        }

        // 3. Rango (400)
        if (request.NotaSugerida > pregunta.PuntajeMaximo)
            return Invalido($"La nota sugerida ({request.NotaSugerida}) supera el puntaje máximo de la pregunta ({pregunta.PuntajeMaximo}).");

        // 4. Idempotencia por (entrega, pregunta): no se pisa una nota ya registrada.
        var calificaciones = await _repositorio.ListarCalificacionesAsync(entrega.Id, ct);
        var existente = calificaciones.FirstOrDefault(c => c.PreguntaId == request.PreguntaId);
        if (existente is not null)
        {
            return ResultadoOperacion<CalificacionSugeridaRegistrada>.Ok(new(
                entrega.Id, existente.PreguntaId, existente.NotaSugerida, true, entrega.Estado.ToString()));
        }

        await _repositorio.AgregarCalificacionAsync(new CalificacionSugerida
        {
            Id = Guid.NewGuid(),
            InstitucionId = entrega.InstitucionId,
            EntregaId = entrega.Id,
            PreguntaId = request.PreguntaId,
            NotaSugerida = request.NotaSugerida,
            Justificacion = request.Justificacion.Trim(),
            DesgloseJson = request.DesgloseJson,
            FechaRegistro = DateTimeOffset.UtcNow
        }, ct);

        // 5. Si ya están todas las abiertas respondidas, la entrega pasa a Corregida.
        var calificadas = calificaciones.Select(c => c.PreguntaId).Append(request.PreguntaId).ToHashSet();
        var pendientes = PreguntasCorreccion.AbiertasRespondidas(evaluacion.PreguntasJson, entrega.RespuestasJson)
            .Count(x => !calificadas.Contains(x.Pregunta.Id));
        if (pendientes == 0 && entrega.Estado != EstadoEntregaExamen.Corregida)
        {
            entrega.Estado = EstadoEntregaExamen.Corregida;
            entrega.Version++;
        }

        try
        {
            await _repositorio.GuardarCambiosAsync(ct);
        }
        catch (ConflictoConcurrenciaException)
        {
            return ResultadoOperacion<CalificacionSugeridaRegistrada>.Conflicto(
                "La entrega fue modificada al mismo tiempo. Reintentá el registro.");
        }

        return ResultadoOperacion<CalificacionSugeridaRegistrada>.Ok(new(
            entrega.Id, request.PreguntaId, request.NotaSugerida, false, entrega.Estado.ToString()));
    }

    private static ResultadoOperacion<CalificacionSugeridaRegistrada> Invalido(string error) =>
        ResultadoOperacion<CalificacionSugeridaRegistrada>.Invalido(error);
}
