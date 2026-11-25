using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMetricasEvaluacion;

public sealed record GetMetricasEvaluacionQuery(
    Guid EvaluacionId,
    Guid InstitucionId) : IRequest<ResultadoOperacion<MetricasEvaluacionDto>>;

public sealed record MetricasEvaluacionDto(
    Guid EvaluacionId,
    int CantidadEntregas,
    decimal? Promedio,
    decimal PuntajeMaximo,
    int CantidadAprobadas,
    Guid? PreguntaMasDificilId,
    IReadOnlyList<MetricaPreguntaDto> Preguntas,
    IReadOnlyList<MetricaAlumnoDto> Alumnos);

public sealed record MetricaPreguntaDto(
    Guid PreguntaId,
    string Enunciado,
    decimal PuntajeMaximo,
    decimal Promedio,
    decimal Porcentaje);

public sealed record MetricaAlumnoDto(
    Guid AlumnoId,
    decimal NotaFinal,
    decimal DiferenciaConPromedio,
    // S3-SA-01: nombre del invitado; null para alumnos con cuenta
    string? NombreInvitado = null);

public sealed class GetMetricasEvaluacionHandler
    : IRequestHandler<GetMetricasEvaluacionQuery, ResultadoOperacion<MetricasEvaluacionDto>>
{
    private readonly IResultadosRepositorio _repositorio;

    public GetMetricasEvaluacionHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<MetricasEvaluacionDto>> Handle(
        GetMetricasEvaluacionQuery request,
        CancellationToken cancellationToken)
    {
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(
            request.EvaluacionId, request.InstitucionId, cancellationToken);

        if (evaluacion is null)
        {
            return ResultadoOperacion<MetricasEvaluacionDto>.NoEncontrado();
        }

        var preguntas = ParsearPreguntas(evaluacion.PreguntasJson);
        var puntajeMaximoExamen = Math.Round(preguntas.Sum(p => p.PuntajeMaximo), 2);

        var todasLasEntregas = await _repositorio.ListarEntregasPorEvaluacionAsync(
            request.EvaluacionId, request.InstitucionId, cancellationToken);

        var cantidadEntregas = todasLasEntregas.Count;

        // Regla contrato S3-CT-01: solo cuentan las entregas con nota Aprobada
        var entregasAprobadas = todasLasEntregas
            .Where(e => e.EstadoNota == EstadoNotaEntrega.Aprobada)
            .ToList();

        if (entregasAprobadas.Count == 0)
        {
            return ResultadoOperacion<MetricasEvaluacionDto>.Ok(
                new MetricasEvaluacionDto(
                    EvaluacionId: request.EvaluacionId,
                    CantidadEntregas: cantidadEntregas,
                    Promedio: null,
                    PuntajeMaximo: puntajeMaximoExamen,
                    CantidadAprobadas: 0,
                    PreguntaMasDificilId: null,
                    Preguntas: Array.Empty<MetricaPreguntaDto>(),
                    Alumnos: Array.Empty<MetricaAlumnoDto>()));
        }

        var entregaIds = entregasAprobadas.Select(e => e.Id).ToList();

        var calificacionesIa = await _repositorio.ListarCalificacionesPorEntregasAsync(
            entregaIds, cancellationToken);
        var calificacionesDocente = await _repositorio.ListarCalificacionesDocentePorEntregasAsync(
            entregaIds, cancellationToken);

        var iaPorEntregaPregunta = calificacionesIa
            .GroupBy(c => (c.EntregaId, c.PreguntaId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FechaRegistro).First().NotaSugerida);

        var docentePorEntregaPregunta = calificacionesDocente
            .GroupBy(c => (c.EntregaId, c.PreguntaId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FechaEdicionUtc).First().PuntajeDocente);

        var sumaPorPregunta = preguntas.ToDictionary(p => p.Id, _ => 0m);
        var notasAlumnos = new List<(Guid AlumnoId, decimal NotaFinal, string? NombreInvitado)>();

        foreach (var entrega in entregasAprobadas)
        {
            var respuestas = ParsearRespuestas(entrega.RespuestasJson);

            foreach (var pregunta in preguntas)
            {
                decimal puntajePregunta = CalcularPuntajePregunta(
                    entrega.Id,
                    pregunta,
                    respuestas,
                    docentePorEntregaPregunta,
                    iaPorEntregaPregunta);

                sumaPorPregunta[pregunta.Id] += puntajePregunta;
            }

            var notaFinalAlumno = Math.Round(entrega.NotaFinal ?? 0m, 2);
            notasAlumnos.Add((entrega.AlumnoId, notaFinalAlumno, entrega.NombreInvitado));
        }

        var cantidadAprobadas = entregasAprobadas.Count;
        var promedioExamen = Math.Round(
            notasAlumnos.Average(n => n.NotaFinal), 2);

        // Métricas para TODAS las preguntas (se mantiene el cálculo por pregunta)
        var metricasPreguntas = new List<MetricaPreguntaDto>();
        foreach (var pregunta in preguntas)
        {
            var promedioPregunta = Math.Round(sumaPorPregunta[pregunta.Id] / cantidadAprobadas, 2);
            var porcentaje = pregunta.PuntajeMaximo > 0m
                ? Math.Round((promedioPregunta / pregunta.PuntajeMaximo) * 100m, 2)
                : 0m;

            metricasPreguntas.Add(new MetricaPreguntaDto(
                PreguntaId: pregunta.Id,
                Enunciado: pregunta.Enunciado,
                PuntajeMaximo: pregunta.PuntajeMaximo,
                Promedio: promedioPregunta,
                Porcentaje: porcentaje));
        }

        // La pregunta más difícil es la de MENOR PORCENTAJE promedio
        Guid? preguntaMasDificilId = metricasPreguntas.Count > 0
            ? metricasPreguntas
                .OrderBy(p => p.Porcentaje)
                .ThenBy(p => p.Promedio)
                .First()
                .PreguntaId
            : null;

        var alumnosDto = notasAlumnos
            .Select(n => new MetricaAlumnoDto(
                AlumnoId: n.AlumnoId,
                NotaFinal: n.NotaFinal,
                DiferenciaConPromedio: Math.Round(n.NotaFinal - promedioExamen, 2),
                NombreInvitado: n.NombreInvitado))
            .ToList();

        return ResultadoOperacion<MetricasEvaluacionDto>.Ok(
            new MetricasEvaluacionDto(
                EvaluacionId: request.EvaluacionId,
                CantidadEntregas: cantidadEntregas,
                Promedio: promedioExamen,
                PuntajeMaximo: puntajeMaximoExamen,
                CantidadAprobadas: cantidadAprobadas,
                PreguntaMasDificilId: preguntaMasDificilId,
                Preguntas: metricasPreguntas,
                Alumnos: alumnosDto));
    }

    private static decimal CalcularPuntajePregunta(
        Guid entregaId,
        PreguntaDefinicion pregunta,
        Dictionary<Guid, string?> respuestas,
        Dictionary<(Guid EntregaId, Guid PreguntaId), decimal> docenteMap,
        Dictionary<(Guid EntregaId, Guid PreguntaId), decimal> iaMap)
    {
        decimal bruto;

        if (docenteMap.TryGetValue((entregaId, pregunta.Id), out var puntajeDocente))
        {
            bruto = puntajeDocente;
        }
        else if (string.Equals(pregunta.Tipo, "OpcionMultiple", StringComparison.OrdinalIgnoreCase))
        {
            if (respuestas.TryGetValue(pregunta.Id, out var opcionElegida)
                && !string.IsNullOrWhiteSpace(opcionElegida)
                && pregunta.OpcionesCorrectas.Contains(opcionElegida.Trim()))
            {
                bruto = pregunta.PuntajeMaximo;
            }
            else
            {
                bruto = 0m;
            }
        }
        else if (iaMap.TryGetValue((entregaId, pregunta.Id), out var puntajeIa))
        {
            bruto = puntajeIa;
        }
        else
        {
            bruto = 0m;
        }

        if (bruto < 0m) return 0m;
        if (bruto > pregunta.PuntajeMaximo) return pregunta.PuntajeMaximo;
        return bruto;
    }

    private sealed record PreguntaDefinicion(
        Guid Id,
        string Enunciado,
        string Tipo,
        decimal PuntajeMaximo,
        HashSet<string> OpcionesCorrectas);

    private static List<PreguntaDefinicion> ParsearPreguntas(string preguntasJson)
    {
        var lista = new List<PreguntaDefinicion>();
        if (string.IsNullOrWhiteSpace(preguntasJson)) return lista;

        try
        {
            using var doc = JsonDocument.Parse(preguntasJson);
            if (!doc.RootElement.TryGetProperty("preguntas", out var preguntasElem)
                || preguntasElem.ValueKind != JsonValueKind.Array)
            {
                return lista;
            }

            foreach (var p in preguntasElem.EnumerateArray())
            {
                if (!p.TryGetProperty("id", out var idProp)
                    || !Guid.TryParse(idProp.GetString(), out var id))
                {
                    continue;
                }

                var enunciado = p.TryGetProperty("enunciado", out var enProp)
                    ? enProp.GetString() ?? string.Empty
                    : string.Empty;

                var tipo = p.TryGetProperty("tipo", out var tipoProp)
                    ? tipoProp.GetString() ?? string.Empty
                    : string.Empty;

                var puntajeMaximo = p.TryGetProperty("puntajeMaximo", out var pmProp)
                    && pmProp.TryGetDecimal(out var pm)
                    ? pm
                    : 0m;

                var opcionesCorrectas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (p.TryGetProperty("opciones", out var opsProp)
                    && opsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var op in opsProp.EnumerateArray())
                    {
                        var esCorrecta = op.TryGetProperty("esCorrecta", out var ecProp)
                            && ecProp.ValueKind == JsonValueKind.True;

                        if (esCorrecta && op.TryGetProperty("id", out var opIdProp))
                        {
                            var opId = opIdProp.GetString();
                            if (!string.IsNullOrWhiteSpace(opId))
                            {
                                opcionesCorrectas.Add(opId.Trim());
                            }
                        }
                    }
                }

                lista.Add(new PreguntaDefinicion(id, enunciado, tipo, puntajeMaximo, opcionesCorrectas));
            }
        }
        catch
        {
            // JSON inválido devuelve lista vacía
        }

        return lista;
    }

    private static Dictionary<Guid, string?> ParsearRespuestas(string respuestasJson)
    {
        var dict = new Dictionary<Guid, string?>();
        if (string.IsNullOrWhiteSpace(respuestasJson)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(respuestasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return dict;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (!elem.TryGetProperty("preguntaId", out var pid)
                    || !Guid.TryParse(pid.GetString(), out var pGuid))
                {
                    continue;
                }

                var opcion = elem.TryGetProperty("opcionElegidaId", out var oProp)
                    ? oProp.GetString()
                    : null;
                var resp = elem.TryGetProperty("respuesta", out var rProp)
                    ? rProp.GetString()
                    : null;
                dict[pGuid] = opcion ?? resp;
            }
        }
        catch
        {
            // JSON inválido devuelve diccionario vacío
        }

        return dict;
    }
}