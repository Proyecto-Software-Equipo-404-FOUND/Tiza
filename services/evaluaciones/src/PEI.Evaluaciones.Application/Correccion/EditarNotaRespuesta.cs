using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Correccion;

public record DesgloseItemDto(string Criterio, decimal Puntaje);

// S3-EV-03 (#224): El docente edita el puntaje de una pregunta de una entrega.
public record EditarNotaRespuestaCommand(
    Guid EvaluacionId,
    Guid EntregaId,
    Guid PreguntaId,
    Guid InstitucionId,
    Guid DocenteId,
    string? Rol,
    decimal Puntaje,
    string? Comentario,
    List<DesgloseItemDto>? DesglosePorCriterio) : IRequest<ResultadoOperacion<RespuestaEntregaDto>>;

public class EditarNotaRespuestaHandler
    : IRequestHandler<EditarNotaRespuestaCommand, ResultadoOperacion<RespuestaEntregaDto>>
{
    private readonly IResultadosRepositorio _repositorio;

    public EditarNotaRespuestaHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<RespuestaEntregaDto>> Handle(
        EditarNotaRespuestaCommand request,
        CancellationToken ct)
    {
        // 1. Permisos (403): Solo Docente
        if (request.Rol != "Docente")
        {
            return ResultadoOperacion<RespuestaEntregaDto>.Invalido("Solo docentes pueden editar la nota.");
        }

        // 2. Existencia de Evaluación (404)
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(request.EvaluacionId, request.InstitucionId, ct);
        if (evaluacion is null)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.NoEncontrado();
        }

        // Permisos de pertenencia (403): Solo el docente dueño del examen
        if (evaluacion.DocenteId.HasValue && evaluacion.DocenteId.Value != request.DocenteId)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.Invalido("Solo el docente que creó el examen puede editar la nota.");
        }

        // 3. Existencia de Entrega (404)
        var entrega = await _repositorio.ObtenerEntregaPorIdAsync(
            request.EvaluacionId, request.EntregaId, request.InstitucionId, ct);
        if (entrega is null)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.NoEncontrado();
        }

        // 4. Inmutabilidad si ya está aprobada (409 NOTA_YA_APROBADA)
        if (entrega.EstadoNota == EstadoNotaEntrega.Aprobada || entrega.FechaAprobacionUtc.HasValue)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.Conflicto("La nota ya está aprobada y no se puede editar.");
        }

        // 5. Validar que la pregunta exista en la evaluación (404)
        var preguntaInfo = ObtenerPregunta(evaluacion.PreguntasJson, request.PreguntaId);
        if (preguntaInfo is null)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.NoEncontrado();
        }

        // 6. Validaciones de Puntaje y Desglose (400)
        if (request.Puntaje < 0 || request.Puntaje > preguntaInfo.PuntajeMaximo)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.Invalido(
                $"El puntaje tiene que estar entre 0 y {preguntaInfo.PuntajeMaximo}.");
        }

        if (request.Comentario?.Length > 1000)
        {
            return ResultadoOperacion<RespuestaEntregaDto>.Invalido(
                "El comentario no puede superar los 1000 caracteres.");
        }

        if (request.DesglosePorCriterio is not null && request.DesglosePorCriterio.Count > 0)
        {
            if (request.DesglosePorCriterio.Any(d => d.Puntaje < 0))
            {
                return ResultadoOperacion<RespuestaEntregaDto>.Invalido("Los puntajes de los criterios no pueden ser negativos.");
            }

            var sumaCriterios = request.DesglosePorCriterio.Sum(d => d.Puntaje);
            if (sumaCriterios != request.Puntaje)
            {
                return ResultadoOperacion<RespuestaEntregaDto>.Invalido(
                    "La suma del desglose por criterio tiene que coincidir con el puntaje.");
            }
        }

        // 7. Persistir Calificación Docente
        var desgloseJson = request.DesglosePorCriterio is not null && request.DesglosePorCriterio.Count > 0
            ? JsonSerializer.Serialize(request.DesglosePorCriterio)
            : null;

        var calificacionDocente = new CalificacionDocente
        {
            Id = Guid.NewGuid(),
            InstitucionId = request.InstitucionId,
            EntregaId = request.EntregaId,
            PreguntaId = request.PreguntaId,
            PuntajeDocente = request.Puntaje,
            ComentarioDocente = request.Comentario?.Trim(),
            DesgloseJson = desgloseJson,
            FechaEdicionUtc = DateTimeOffset.UtcNow
        };

        await _repositorio.GuardarCalificacionDocenteAsync(calificacionDocente, ct);

        // 8. Actualizar estado de la entrega a Editada y subir Version
        entrega.EstadoNota = EstadoNotaEntrega.Editada;
        entrega.Version++;
        await _repositorio.GuardarCambiosAsync(ct);

        // 9. Construir y devolver la respuesta actualizada
        return ResultadoOperacion<RespuestaEntregaDto>.Ok(new RespuestaEntregaDto(
            preguntaInfo.Id,
            preguntaInfo.Enunciado,
            preguntaInfo.Tipo,
            preguntaInfo.PuntajeMaximo,
            Respuesta: null,
            OpcionElegidaTexto: null,
            EsCorrecta: null,
            PuntajeObtenido: null,
            NotaSugerida: null,
            JustificacionIA: null,
            DesglosePorCriterio: null,
            EstadoCorreccion: "Editada",
            PuntajeDocente: request.Puntaje,
            ComentarioDocente: request.Comentario?.Trim()
        ));
    }

    private static PreguntaSimple? ObtenerPregunta(string preguntasJson, Guid preguntaId)
    {
        if (string.IsNullOrWhiteSpace(preguntasJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(preguntasJson);
            if (!doc.RootElement.TryGetProperty("preguntas", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var elem in arr.EnumerateArray())
            {
                if (elem.TryGetProperty("id", out var idProp) &&
                    Guid.TryParse(idProp.GetString(), out var id) &&
                    id == preguntaId)
                {
                    var enunciado = elem.TryGetProperty("enunciado", out var e) ? e.GetString() ?? "" : "";
                    var tipo = elem.TryGetProperty("tipo", out var t) ? t.GetString() ?? "Abierta" : "Abierta";
                    var max = elem.TryGetProperty("puntajeMaximo", out var pm) && pm.TryGetDecimal(out var m) ? m : 0m;
                    return new PreguntaSimple(id, enunciado, tipo, max);
                }
            }
        }
        catch { }

        return null;
    }

    private record PreguntaSimple(Guid Id, string Enunciado, string Tipo, decimal PuntajeMaximo);
}