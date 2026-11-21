using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMetricasEvaluacion;

// ==========================================
// DTOs DE RESPUESTA (S3-EV-05 - #240)
// ==========================================

public record PreguntaMasDificilDto(
    Guid PreguntaId,
    string Enunciado,
    string Tipo,
    decimal PuntajePromedio,
    decimal PuntajeMaximo,
    double PorcentajeAcierto);

public record MetricaAlumnoDto(
    Guid AlumnoId,
    Guid EntregaId,
    string EstadoNota,
    decimal PuntajeObtenido,
    decimal PuntajeMaximo,
    decimal DiferenciaConPromedio,
    string ComparacionConPromedio);

public record MetricasEvaluacionDto(
    Guid EvaluacionId,
    string Titulo,
    int CantidadEntregas,
    decimal? PromedioExamen,
    decimal PuntajeMaximoExamen,
    PreguntaMasDificilDto? PreguntaMasDificil,
    List<MetricaAlumnoDto> Alumnos);

// ==========================================
// QUERY
// ==========================================

public record GetMetricasEvaluacionQuery(Guid EvaluacionId, Guid InstitucionId)
    : IRequest<ResultadoOperacion<MetricasEvaluacionDto>>;

// ==========================================
// HANDLER
// ==========================================

public class GetMetricasEvaluacionHandler
    : IRequestHandler<GetMetricasEvaluacionQuery, ResultadoOperacion<MetricasEvaluacionDto>>
{
    private readonly IResultadosRepositorio _repositorio;

    public GetMetricasEvaluacionHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<MetricasEvaluacionDto>> Handle(
        GetMetricasEvaluacionQuery request,
        CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(
            request.EvaluacionId,
            request.InstitucionId,
            ct);

        if (evaluacion is null)
        {
            return ResultadoOperacion<MetricasEvaluacionDto>.NoEncontrado();
        }

        var preguntas = ParsearPreguntas(evaluacion.PreguntasJson);
        var puntajeMaximoExamen = preguntas.Sum(p => p.PuntajeMaximo);

        var entregas = await _repositorio.ListarEntregasPorEvaluacionAsync(
            request.EvaluacionId,
            request.InstitucionId,
            ct);

        if (entregas.Count == 0)
        {
            return ResultadoOperacion<MetricasEvaluacionDto>.Ok(new MetricasEvaluacionDto(
                evaluacion.Id,
                evaluacion.Titulo,
                CantidadEntregas: 0,
                PromedioExamen: null,
                PuntajeMaximoExamen: puntajeMaximoExamen,
                PreguntaMasDificil: null,
                Alumnos: new List<MetricaAlumnoDto>()));
        }

        var entregaIds = entregas.Select(e => e.Id).ToList();
        var calificacionesIa = await _repositorio.ListarCalificacionesPorEntregasAsync(entregaIds, ct);
        var calificacionesDocente = await _repositorio.ListarCalificacionesDocentePorEntregasAsync(entregaIds, ct);

        // Acumuladores por pregunta para detectar la de menor puntaje promedio
        var sumaPorPregunta = preguntas.ToDictionary(p => p.Id, _ => 0m);
        var conteoPorPregunta = preguntas.ToDictionary(p => p.Id, _ => 0);

        // Puntaje total calculado por cada entrega/alumno
        var puntajesPorEntrega = new List<(EntregaExamen Entrega, decimal PuntajeTotal)>();

        foreach (var entrega in entregas)
        {
            var respuestasEntrega = ParsearRespuestasEntrega(entrega.RespuestasJson);
            var docPorPregunta = calificacionesDocente
                .Where(c => c.EntregaId == entrega.Id)
                .ToDictionary(c => c.PreguntaId, c => c.PuntajeDocente);
            var iaPorPregunta = calificacionesIa
                .Where(c => c.EntregaId == entrega.Id)
                .ToDictionary(c => c.PreguntaId, c => c.NotaSugerida);

            decimal sumaPreguntasEntrega = 0m;

            foreach (var pregunta in preguntas)
            {
                decimal puntajePregunta = CalcularPuntajePregunta(
                    pregunta,
                    respuestasEntrega,
                    docPorPregunta,
                    iaPorPregunta);

                sumaPreguntasEntrega += puntajePregunta;
                sumaPorPregunta[pregunta.Id] += puntajePregunta;
                conteoPorPregunta[pregunta.Id]++;
            }

            // Si la entrega ya fue aprobada con NotaFinal explícita, se respeta NotaFinal;
            // caso contrario se usa la suma vigente de puntajes (docente > IA > MC).
            var puntajeAlumno = (entrega.EstadoNota == EstadoNotaEntrega.Aprobada && entrega.NotaFinal.HasValue)
                ? entrega.NotaFinal.Value
                : sumaPreguntasEntrega;

            puntajesPorEntrega.Add((entrega, Math.Round(puntajeAlumno, 2)));
        }

        // 1. Promedio general del examen
        var promedioExamen = Math.Round(puntajesPorEntrega.Average(x => x.PuntajeTotal), 2);

        // 2. Pregunta con menor puntaje promedio (la más difícil)
        PreguntaMasDificilDto? preguntaMasDificil = null;
        if (preguntas.Count > 0)
        {
            var candidata = preguntas
                .Select(p =>
                {
                    var total = conteoPorPregunta[p.Id];
                    var prom = total > 0 ? Math.Round(sumaPorPregunta[p.Id] / total, 2) : 0m;
                    var porcentaje = p.PuntajeMaximo > 0
                        ? Math.Round((double)(prom / p.PuntajeMaximo) * 100.0, 2)
                        : 0.0;

                    return new PreguntaMasDificilDto(
                        p.Id,
                        p.Enunciado,
                        p.Tipo,
                        prom,
                        p.PuntajeMaximo,
                        porcentaje);
                })
                .OrderBy(x => x.PuntajePromedio)
                .ThenBy(x => x.PorcentajeAcierto)
                .First();

            preguntaMasDificil = candidata;
        }

        // 3. Cada alumno comparado contra el promedio del curso
        var alumnos = puntajesPorEntrega
            .Select(item =>
            {
                var diferencia = Math.Round(item.PuntajeTotal - promedioExamen, 2);
                var comparacion = diferencia > 0m
                    ? "PorEncimaDelPromedio"
                    : diferencia < 0m
                        ? "PorDebajoDelPromedio"
                        : "EnElPromedio";

                return new MetricaAlumnoDto(
                    item.Entrega.AlumnoId,
                    item.Entrega.Id,
                    item.Entrega.EstadoNota.ToString(),
                    item.PuntajeTotal,
                    puntajeMaximoExamen,
                    diferencia,
                    comparacion);
            })
            .OrderByDescending(a => a.PuntajeObtenido)
            .ToList();

        var resultado = new MetricasEvaluacionDto(
            evaluacion.Id,
            evaluacion.Titulo,
            entregas.Count,
            promedioExamen,
            puntajeMaximoExamen,
            preguntaMasDificil,
            alumnos);

        return ResultadoOperacion<MetricasEvaluacionDto>.Ok(resultado);
    }

    private static decimal CalcularPuntajePregunta(
        PreguntaInfo pregunta,
        Dictionary<Guid, string?> respuestasEntrega,
        Dictionary<Guid, decimal> docPorPregunta,
        Dictionary<Guid, decimal> iaPorPregunta)
    {
        // Prioridad 1: Edición explícita del docente
        if (docPorPregunta.TryGetValue(pregunta.Id, out var puntajeDocente))
        {
            return puntajeDocente;
        }

        // Prioridad 2: Opción múltiple evaluada automáticamente
        if (string.Equals(pregunta.Tipo, "OpcionMultiple", StringComparison.OrdinalIgnoreCase))
        {
            if (respuestasEntrega.TryGetValue(pregunta.Id, out var opcionIdStr) &&
                !string.IsNullOrWhiteSpace(opcionIdStr) &&
                Guid.TryParse(opcionIdStr, out var opcionGuid) &&
                pregunta.OpcionesCorrectas.Contains(opcionGuid))
            {
                return pregunta.PuntajeMaximo;
            }

            return 0m;
        }

        // Prioridad 3: Nota sugerida por IA para pregunta abierta
        if (iaPorPregunta.TryGetValue(pregunta.Id, out var notaIa))
        {
            return notaIa;
        }

        return 0m;
    }

    private static List<PreguntaInfo> ParsearPreguntas(string preguntasJson)
    {
        var lista = new List<PreguntaInfo>();
        if (string.IsNullOrWhiteSpace(preguntasJson)) return lista;

        try
        {
            using var doc = JsonDocument.Parse(preguntasJson);
            if (!doc.RootElement.TryGetProperty("preguntas", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return lista;

            foreach (var elem in arr.EnumerateArray())
            {
                if (!elem.TryGetProperty("id", out var idProp) || !Guid.TryParse(idProp.GetString(), out var id))
                    continue;

                var enunciado = elem.TryGetProperty("enunciado", out var e) ? e.GetString() ?? "" : "";
                var tipo = elem.TryGetProperty("tipo", out var t) ? t.GetString() ?? "Abierta" : "Abierta";
                var puntajeMaximo = elem.TryGetProperty("puntajeMaximo", out var pm) && pm.TryGetDecimal(out var max)
                    ? max
                    : 0m;

                var correctas = new HashSet<Guid>();
                if (elem.TryGetProperty("opciones", out var opcs) && opcs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in opcs.EnumerateArray())
                    {
                        if (o.TryGetProperty("id", out var oid) &&
                            Guid.TryParse(oid.GetString(), out var oGuid) &&
                            o.TryGetProperty("esCorrecta", out var c) &&
                            c.GetBoolean())
                        {
                            correctas.Add(oGuid);
                        }
                    }
                }

                lista.Add(new PreguntaInfo(id, enunciado, tipo, puntajeMaximo, correctas));
            }
        }
        catch
        {
            // JSON inválido devuelve lista vacía sin romper el endpoint
        }

        return lista;
    }

    private static Dictionary<Guid, string?> ParsearRespuestasEntrega(string respuestasJson)
    {
        var dict = new Dictionary<Guid, string?>();
        if (string.IsNullOrWhiteSpace(respuestasJson)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(respuestasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return dict;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (!elem.TryGetProperty("preguntaId", out var pid) || !Guid.TryParse(pid.GetString(), out var pGuid))
                    continue;

                var opcion = elem.TryGetProperty("opcionElegidaId", out var oProp) ? oProp.GetString() : null;
                var resp = elem.TryGetProperty("respuesta", out var rProp) ? rProp.GetString() : null;
                dict[pGuid] = opcion ?? resp;
            }
        }
        catch
        {
            // JSON inválido devuelve diccionario vacío
        }

        return dict;
    }

    private record PreguntaInfo(
        Guid Id,
        string Enunciado,
        string Tipo,
        decimal PuntajeMaximo,
        HashSet<Guid> OpcionesCorrectas);
}