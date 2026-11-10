using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public sealed record RecibirLoteEntregasCommand(
    Guid SalaIdRuta,
    LoteEntregasRequest Lote)
    : IRequest<ResultadoOperacion<LoteEntregasResultado>>;

public sealed record LoteEntregasRequest(
    int Version,
    Guid LoteId,
    Guid SalaId,
    Guid EvaluacionId,
    Guid InstitucionId,
    DateTimeOffset FinalizadaUtc,
    string MotivoCierre,
    List<EntregaLoteRequest> Entregas);

public sealed record EntregaLoteRequest(
    Guid AlumnoId,
    List<RespuestaLoteRequest> Respuestas,
    List<JsonElement> AlertasFoco);

public sealed record RespuestaLoteRequest(
    Guid PreguntaId,
    string Respuesta,
    int Secuencia,
    DateTimeOffset RecibidaUtc);

public sealed record LoteEntregasResultado(
    Guid LoteId,
    Guid SalaId,
    int EntregasRecibidas,
    int EntregasNuevas,
    int EntregasYaExistentes,
    List<EntregaLoteResultado> Entregas);

public sealed record EntregaLoteResultado(
    Guid AlumnoId,
    Guid EntregaId,
    string Estado,
    decimal? NotaMultipleChoice);

public sealed class RecibirLoteEntregasHandler
    : IRequestHandler<RecibirLoteEntregasCommand, ResultadoOperacion<LoteEntregasResultado>>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IEvaluacionRepositorio _repositorio;

    public RecibirLoteEntregasHandler(IEvaluacionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<LoteEntregasResultado>> Handle(
        RecibirLoteEntregasCommand request,
        CancellationToken ct)
    {
        var lote = request.Lote;

        if (lote is null ||
            lote.Version != 1 ||
            lote.LoteId == Guid.Empty ||
            lote.SalaId == Guid.Empty ||
            lote.SalaId != request.SalaIdRuta ||
            lote.EvaluacionId == Guid.Empty ||
            lote.InstitucionId == Guid.Empty ||
            lote.FinalizadaUtc == default ||
            (lote.MotivoCierre != "Docente" &&
             lote.MotivoCierre != "TiempoAgotado") ||
            lote.Entregas is null ||
            lote.Entregas.Count > 100)
        {
            return ResultadoOperacion<LoteEntregasResultado>.Invalido(
                "El lote no cumple el contrato o su salaId no coincide con la ruta.");
        }

        if (lote.Entregas.Any(e => e is null ||
            e.AlumnoId == Guid.Empty ||
            e.Respuestas is null ||
            e.AlertasFoco is null))
        {
            return ResultadoOperacion<LoteEntregasResultado>.Invalido(
                "Una entrega del lote tiene datos obligatorios inválidos.");
        }

        if (lote.Entregas
            .GroupBy(e => e.AlumnoId)
            .Any(grupo => grupo.Count() > 1))
        {
            return ResultadoOperacion<LoteEntregasResultado>.Invalido(
                "El lote contiene más de una entrega para el mismo alumno.");
        }

        var evaluacion = await _repositorio.ObtenerPorIdAsync(
            lote.EvaluacionId,
            lote.InstitucionId,
            ct);

        if (evaluacion is null || evaluacion.Estado != EstadoEvaluacion.Publicada)
        {
            return ResultadoOperacion<LoteEntregasResultado>.NoEncontrado();
        }

        List<PreguntaEvaluacion> preguntas;
        try
        {
            preguntas = LeerPreguntas(evaluacion.PreguntasJson);
        }
        catch (Exception ex) when (
            ex is JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException)
        {
            return ResultadoOperacion<LoteEntregasResultado>.Invalido(
                "No se pudieron leer las preguntas de la evaluación.");
        }

        var preguntasPorId = preguntas.ToDictionary(p => p.Id);

        foreach (var entrega in lote.Entregas)
        {
            if (entrega.Respuestas.Any(r =>
                r.PreguntaId == Guid.Empty ||
                string.IsNullOrWhiteSpace(r.Respuesta) ||
                r.Secuencia < 1 ||
                !preguntasPorId.ContainsKey(r.PreguntaId)))
            {
                return ResultadoOperacion<LoteEntregasResultado>.Invalido(
                    "Una respuesta no corresponde a una pregunta válida de la evaluación.");
            }
        }

        var existentes = await _repositorio.ListarEntregasPorSalaAsync(
            lote.SalaId,
            ct);

        var existentesPorAlumno = existentes.ToDictionary(e => e.AlumnoId);
        var resultados = new List<EntregaLoteResultado>();
        var nuevas = 0;
        var yaExistentes = 0;

        foreach (var entrada in lote.Entregas)
        {
            if (existentesPorAlumno.TryGetValue(entrada.AlumnoId, out var guardada))
            {
                yaExistentes++;
                resultados.Add(new EntregaLoteResultado(
                    guardada.AlumnoId,
                    guardada.Id,
                    guardada.Estado.ToString(),
                    guardada.NotaMultipleChoice));
                continue;
            }

            var respuestasAbiertas = entrada.Respuestas.Any(r =>
                preguntasPorId[r.PreguntaId].Tipo == "Abierta");

            var notaMultipleChoice = CalcularNotaMultipleChoice(
                preguntas,
                entrada.Respuestas);

            var nueva = new EntregaExamen
            {
                Id = Guid.NewGuid(),
                InstitucionId = lote.InstitucionId,
                EvaluacionId = lote.EvaluacionId,
                SalaId = lote.SalaId,
                AlumnoId = entrada.AlumnoId,
                RespuestasJson = JsonSerializer.Serialize(entrada.Respuestas, JsonOptions),
                AlertasFocoJson = JsonSerializer.Serialize(entrada.AlertasFoco, JsonOptions),
                NotaMultipleChoice = notaMultipleChoice,
                Estado = respuestasAbiertas
                    ? EstadoEntregaExamen.PendienteCorreccion
                    : EstadoEntregaExamen.Corregida,
                FechaEntrega = lote.FinalizadaUtc
            };

            await _repositorio.AgregarEntregaAsync(nueva, ct);
            existentesPorAlumno.Add(nueva.AlumnoId, nueva);
            nuevas++;

            resultados.Add(new EntregaLoteResultado(
                nueva.AlumnoId,
                nueva.Id,
                nueva.Estado.ToString(),
                nueva.NotaMultipleChoice));
        }

        if (nuevas > 0)
        {
            await _repositorio.GuardarCambiosAsync(ct);
        }

        return ResultadoOperacion<LoteEntregasResultado>.Ok(
            new LoteEntregasResultado(
                lote.LoteId,
                lote.SalaId,
                lote.Entregas.Count,
                nuevas,
                yaExistentes,
                resultados));
    }

    private static decimal? CalcularNotaMultipleChoice(
        List<PreguntaEvaluacion> preguntas,
        List<RespuestaLoteRequest> respuestas)
    {
        var preguntasMultipleChoice = preguntas
            .Where(p => p.Tipo == "OpcionMultiple")
            .ToList();

        if (preguntasMultipleChoice.Count == 0)
        {
            return null;
        }

        decimal nota = 0;

        foreach (var pregunta in preguntasMultipleChoice)
        {
            var respuesta = respuestas
                .Where(r => r.PreguntaId == pregunta.Id)
                .OrderByDescending(r => r.Secuencia)
                .FirstOrDefault();

            if (respuesta is null ||
                !Guid.TryParse(respuesta.Respuesta, out var opcionId))
            {
                continue;
            }

            if (pregunta.Opciones.Any(o => o.Id == opcionId && o.EsCorrecta))
            {
                nota += pregunta.PuntajeMaximo;
            }
        }

        return nota;
    }

    private static List<PreguntaEvaluacion> LeerPreguntas(string preguntasJson)
    {
        using var documento = JsonDocument.Parse(preguntasJson);
        var preguntasJsonArray = documento.RootElement.GetProperty("preguntas");
        var resultado = new List<PreguntaEvaluacion>();

        foreach (var pregunta in preguntasJsonArray.EnumerateArray())
        {
            var opciones = new List<OpcionEvaluacion>();

            if (pregunta.TryGetProperty("opciones", out var opcionesJson) &&
                opcionesJson.ValueKind == JsonValueKind.Array)
            {
                foreach (var opcion in opcionesJson.EnumerateArray())
                {
                    opciones.Add(new OpcionEvaluacion(
                        opcion.GetProperty("id").GetGuid(),
                        opcion.GetProperty("esCorrecta").GetBoolean()));
                }
            }

            resultado.Add(new PreguntaEvaluacion(
                pregunta.GetProperty("id").GetGuid(),
                pregunta.GetProperty("tipo").GetString() ?? string.Empty,
                pregunta.GetProperty("puntajeMaximo").GetDecimal(),
                opciones));
        }

        return resultado;
    }

    private sealed record PreguntaEvaluacion(
        Guid Id,
        string Tipo,
        decimal PuntajeMaximo,
        List<OpcionEvaluacion> Opciones);

    private sealed record OpcionEvaluacion(Guid Id, bool EsCorrecta);
}