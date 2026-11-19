using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Correccion;

public record NotaAprobadaDto(
    Guid EntregaId,
    decimal NotaFinal,
    decimal PuntajeMaximo,
    Guid AprobadaPorId,
    DateTimeOffset FechaAprobacionUtc);

// S3-EV-03 (#224): El docente aprueba la nota final de la entrega.
public record AprobarNotaEntregaCommand(
    Guid EvaluacionId,
    Guid EntregaId,
    Guid InstitucionId,
    Guid DocenteId,
    string? Rol) : IRequest<ResultadoOperacion<NotaAprobadaDto>>;

public class AprobarNotaEntregaHandler
    : IRequestHandler<AprobarNotaEntregaCommand, ResultadoOperacion<NotaAprobadaDto>>
{
    private readonly IResultadosRepositorio _repositorio;

    public AprobarNotaEntregaHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<NotaAprobadaDto>> Handle(
        AprobarNotaEntregaCommand request,
        CancellationToken ct)
    {
        // 1. Permisos: Solo Docente
        if (request.Rol != "Docente")
        {
            return ResultadoOperacion<NotaAprobadaDto>.Invalido("Solo el docente que creó el examen puede aprobar la nota.");
        }

        // 2. Existencia de la Evaluación (404)
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(request.EvaluacionId, request.InstitucionId, ct);
        if (evaluacion is null)
        {
            return ResultadoOperacion<NotaAprobadaDto>.NoEncontrado();
        }

        // Permisos: Solo el docente dueño del examen
        if (evaluacion.DocenteId.HasValue && evaluacion.DocenteId.Value != request.DocenteId)
        {
            return ResultadoOperacion<NotaAprobadaDto>.Invalido("Solo el docente que creó el examen puede aprobar la nota.");
        }

        // 3. Existencia de la Entrega (404)
        var entrega = await _repositorio.ObtenerEntregaPorIdAsync(
            request.EvaluacionId, request.EntregaId, request.InstitucionId, ct);
        if (entrega is null)
        {
            return ResultadoOperacion<NotaAprobadaDto>.NoEncontrado();
        }

        // 4. Si ya estaba aprobada -> 409 NOTA_YA_APROBADA
        if (entrega.EstadoNota == EstadoNotaEntrega.Aprobada || entrega.FechaAprobacionUtc.HasValue)
        {
            return ResultadoOperacion<NotaAprobadaDto>.Conflicto("La nota ya está aprobada.");
        }

        // 5. Cargar preguntas, calificaciones sugeridas por IA y ediciones del docente
        var estructura = ParsearPreguntas(evaluacion.PreguntasJson);
        var calificacionesIa = (await _repositorio.ListarCalificacionesPorEntregaAsync(entrega.Id, ct))
            .ToDictionary(c => c.PreguntaId);
        var calificacionesDocente = (await _repositorio.ListarCalificacionesDocentePorEntregaAsync(entrega.Id, ct))
            .ToDictionary(c => c.PreguntaId);
        var respuestasGuardadas = LeerRespuestas(entrega.RespuestasJson);

        // 6. Verificar si faltan correcciones en abiertas respondidas (409 FALTAN_CORRECCIONES)
        foreach (var p in estructura)
        {
            if (p.Tipo == "Abierta")
            {
                respuestasGuardadas.TryGetValue(p.Id, out var respuestaTexto);
                var respondio = !string.IsNullOrWhiteSpace(respuestaTexto);

                // Si respondió pero no fue corregida por IA y el docente no la editó -> 409
                if (respondio && !calificacionesDocente.ContainsKey(p.Id) && !calificacionesIa.ContainsKey(p.Id))
                {
                    return ResultadoOperacion<NotaAprobadaDto>.Conflicto("Hay preguntas abiertas sin corregir.");
                }
            }
        }

        // 7. Cálculo de la Nota Final
        decimal notaFinal = 0m;
        decimal puntajeMaximo = estructura.Sum(p => p.PuntajeMaximo);

        foreach (var p in estructura)
        {
            if (calificacionesDocente.TryGetValue(p.Id, out var califDocente))
            {
                // La nota editada por el docente manda sobre todo
                notaFinal += califDocente.PuntajeDocente;
            }
            else if (p.Tipo == "OpcionMultiple")
            {
                respuestasGuardadas.TryGetValue(p.Id, out var opcionElegida);
                if (!string.IsNullOrWhiteSpace(opcionElegida) &&
                    Guid.TryParse(opcionElegida, out var opcionId) &&
                    p.OpcionesCorrectas.Contains(opcionId))
                {
                    notaFinal += p.PuntajeMaximo;
                }
            }
            else if (p.Tipo == "Abierta")
            {
                if (calificacionesIa.TryGetValue(p.Id, out var califIa))
                {
                    notaFinal += califIa.NotaSugerida;
                }
            }
        }

        // 8. Persistir la Aprobación
        var ahoraUtc = DateTimeOffset.UtcNow;
        entrega.EstadoNota = EstadoNotaEntrega.Aprobada;
        entrega.NotaFinal = notaFinal;
        entrega.PuntajeMaximo = puntajeMaximo;
        entrega.DocenteAprobadorId = request.DocenteId;
        entrega.FechaAprobacionUtc = ahoraUtc;
        entrega.Version++;

        await _repositorio.GuardarCambiosAsync(ct);

        return ResultadoOperacion<NotaAprobadaDto>.Ok(new NotaAprobadaDto(
            entrega.Id,
            notaFinal,
            puntajeMaximo,
            request.DocenteId,
            ahoraUtc
        ));
    }

    private static List<PreguntaCalculo> ParsearPreguntas(string preguntasJson)
    {
        var lista = new List<PreguntaCalculo>();
        if (string.IsNullOrWhiteSpace(preguntasJson)) return lista;

        try
        {
            using var doc = JsonDocument.Parse(preguntasJson);
            if (!doc.RootElement.TryGetProperty("preguntas", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return lista;

            foreach (var elem in arr.EnumerateArray())
            {
                if (elem.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var id))
                {
                    var tipo = elem.TryGetProperty("tipo", out var t) ? t.GetString() ?? "Abierta" : "Abierta";
                    var max = elem.TryGetProperty("puntajeMaximo", out var pm) && pm.TryGetDecimal(out var m) ? m : 0m;
                    var correctas = new HashSet<Guid>();

                    if (elem.TryGetProperty("opciones", out var opcs) && opcs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var o in opcs.EnumerateArray())
                        {
                            if (o.TryGetProperty("esCorrecta", out var corr) && corr.GetBoolean() &&
                                o.TryGetProperty("id", out var oid) && Guid.TryParse(oid.GetString(), out var oGuid))
                            {
                                correctas.Add(oGuid);
                            }
                        }
                    }

                    lista.Add(new PreguntaCalculo(id, tipo, max, correctas));
                }
            }
        }
        catch { }

        return lista;
    }

    private static Dictionary<Guid, string> LeerRespuestas(string respuestasJson)
    {
        var dict = new Dictionary<Guid, string>();
        if (string.IsNullOrWhiteSpace(respuestasJson)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(respuestasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return dict;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("preguntaId", out var pid) && Guid.TryParse(pid.GetString(), out var pGuid))
                {
                    var r = item.TryGetProperty("respuesta", out var rProp) ? rProp.GetString() : null;
                    var o = item.TryGetProperty("opcionElegidaId", out var oProp) ? oProp.GetString() : null;
                    dict[pGuid] = r ?? o ?? "";
                }
            }
        }
        catch { }

        return dict;
    }

    private record PreguntaCalculo(Guid Id, string Tipo, decimal PuntajeMaximo, HashSet<Guid> OpcionesCorrectas);
}