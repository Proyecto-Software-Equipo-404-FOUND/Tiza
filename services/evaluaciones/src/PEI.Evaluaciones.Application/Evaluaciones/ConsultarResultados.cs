using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

// DTOs según contrato OpenAPI evaluaciones-v1.json (S2-EV-03)
public record AlertasFocoResumenDto(int Amarillas, int Rojas);

public record EntregaResumenDto(
    Guid EntregaId,
    Guid AlumnoId,
    Guid SalaId,
    string Estado,
    bool RespondioAlgo,
    decimal? NotaMultipleChoice,
    decimal? PuntajeMaximoMultipleChoice,
    decimal? NotaSugeridaAbiertas,
    decimal? PuntajeMaximoAbiertas,
    DateTimeOffset FechaEntregaUtc,
    AlertasFocoResumenDto AlertasFoco);

public record DesgloseCriterioDto(string Criterio, decimal Puntaje, string Comentario);

public record RespuestaEntregaDto(
    Guid PreguntaId,
    string Enunciado,
    string Tipo,
    decimal PuntajeMaximo,
    string? Respuesta,
    string? OpcionElegidaTexto,
    bool? EsCorrecta,
    decimal? PuntajeObtenido,
    decimal? NotaSugerida,
    string? JustificacionIA,
    List<DesgloseCriterioDto>? DesglosePorCriterio,
    string EstadoCorreccion);

public record AlertaFocoDetalleDto(
    Guid? PreguntaId,
    string TipoEvento,
    int DuracionMs,
    string Severidad,
    DateTimeOffset RegistradoUtc);

public record EntregaDetalleDto(
    Guid EntregaId,
    Guid AlumnoId,
    Guid SalaId,
    string Estado,
    bool RespondioAlgo,
    decimal? NotaMultipleChoice,
    decimal? PuntajeMaximoMultipleChoice,
    decimal? NotaSugeridaAbiertas,
    decimal? PuntajeMaximoAbiertas,
    DateTimeOffset FechaEntregaUtc,
    AlertasFocoResumenDto AlertasFoco,
    List<RespuestaEntregaDto> Respuestas,
    List<AlertaFocoDetalleDto> Alertas);

// Query para Listar Entregas
public record ListarEntregasEvaluacionQuery(Guid EvaluacionId, Guid InstitucionId)
    : IRequest<ResultadoOperacion<List<EntregaResumenDto>>>;

// Query para Obtener Detalle de Entrega
public record ObtenerEntregaDetalleQuery(Guid EvaluacionId, Guid EntregaId, Guid InstitucionId)
    : IRequest<ResultadoOperacion<EntregaDetalleDto>>;

// Interfaz propia para no tocar IEvaluacionRepositorio ni romper los tests
public interface IResultadosRepositorio
{
    Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct);
    Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct);
    Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct);
    Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct);
    Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct);
}

// Handler que usa IResultadosRepositorio
public class ConsultarResultadosHandler :
    IRequestHandler<ListarEntregasEvaluacionQuery, ResultadoOperacion<List<EntregaResumenDto>>>,
    IRequestHandler<ObtenerEntregaDetalleQuery, ResultadoOperacion<EntregaDetalleDto>>
{
    private readonly IResultadosRepositorio _repositorio;

    public ConsultarResultadosHandler(IResultadosRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<List<EntregaResumenDto>>> Handle(
        ListarEntregasEvaluacionQuery request,
        CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(request.EvaluacionId, request.InstitucionId, ct);
        if (evaluacion == null)
        {
            return ResultadoOperacion<List<EntregaResumenDto>>.NoEncontrado();
        }

        var estructuraPreguntas = ParsearEstructuraPreguntas(evaluacion.PreguntasJson);
        var entregas = await _repositorio.ListarEntregasPorEvaluacionAsync(request.EvaluacionId, request.InstitucionId, ct);

        var entregaIds = entregas.Select(e => e.Id).ToList();
        var calificaciones = await _repositorio.ListarCalificacionesPorEntregasAsync(entregaIds, ct);

        var resumenes = entregas.Select(entrega =>
        {
            var califsEntrega = calificaciones.Where(c => c.EntregaId == entrega.Id).ToList();
            return MapearResumen(entrega, estructuraPreguntas, califsEntrega);
        }).ToList();

        return ResultadoOperacion<List<EntregaResumenDto>>.Ok(resumenes);
    }

    public async Task<ResultadoOperacion<EntregaDetalleDto>> Handle(
        ObtenerEntregaDetalleQuery request,
        CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerEvaluacionAsync(request.EvaluacionId, request.InstitucionId, ct);
        if (evaluacion == null)
        {
            return ResultadoOperacion<EntregaDetalleDto>.NoEncontrado();
        }

        var entrega = await _repositorio.ObtenerEntregaPorIdAsync(request.EvaluacionId, request.EntregaId, request.InstitucionId, ct);
        if (entrega == null)
        {
            return ResultadoOperacion<EntregaDetalleDto>.NoEncontrado();
        }

        var calificaciones = await _repositorio.ListarCalificacionesPorEntregaAsync(entrega.Id, ct);
        var estructuraPreguntas = ParsearEstructuraPreguntas(evaluacion.PreguntasJson);
        var resumen = MapearResumen(entrega, estructuraPreguntas, calificaciones);

        var respuestas = MapearRespuestas(entrega.RespuestasJson, estructuraPreguntas, calificaciones);
        var alertas = MapearAlertasDetalle(entrega.AlertasFocoJson);

        var detalle = new EntregaDetalleDto(
            resumen.EntregaId,
            resumen.AlumnoId,
            resumen.SalaId,
            resumen.Estado,
            resumen.RespondioAlgo,
            resumen.NotaMultipleChoice,
            resumen.PuntajeMaximoMultipleChoice,
            resumen.NotaSugeridaAbiertas,
            resumen.PuntajeMaximoAbiertas,
            resumen.FechaEntregaUtc,
            resumen.AlertasFoco,
            respuestas,
            alertas);

        return ResultadoOperacion<EntregaDetalleDto>.Ok(detalle);
    }

    private static EntregaResumenDto MapearResumen(
        EntregaExamen entrega,
        EstructuraPreguntasEvaluacion estructura,
        List<CalificacionSugerida> calificaciones)
    {
        var alertas = ParsearAlertas(entrega.AlertasFocoJson);
        var respondioAlgo = VerificarSiRespondioAlgo(entrega.RespuestasJson);

        decimal? notaSugeridaAbiertas = null;
        if (estructura.TienePreguntasAbiertas)
        {
            // RN-CMP-03: null mientras falte alguna calificación de IA
            if (estructura.CantidadPreguntasAbiertas > 0 && calificaciones.Count >= estructura.CantidadPreguntasAbiertas)
            {
                notaSugeridaAbiertas = calificaciones.Sum(c => c.NotaSugerida);
            }
        }

        return new EntregaResumenDto(
            entrega.Id,
            entrega.AlumnoId,
            entrega.SalaId,
            entrega.Estado.ToString(),
            respondioAlgo,
            entrega.NotaMultipleChoice,
            estructura.TienePreguntasMultipleChoice ? estructura.PuntajeMaximoMultipleChoice : null,
            notaSugeridaAbiertas,
            estructura.TienePreguntasAbiertas ? estructura.PuntajeMaximoAbiertas : null,
            entrega.FechaEntrega,
            alertas);
    }

    private static AlertasFocoResumenDto ParsearAlertas(string alertasJson)
    {
        if (string.IsNullOrWhiteSpace(alertasJson)) return new AlertasFocoResumenDto(0, 0);

        try
        {
            using var doc = JsonDocument.Parse(alertasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new AlertasFocoResumenDto(0, 0);

            int amarillas = 0;
            int rojas = 0;
            foreach (var elemento in doc.RootElement.EnumerateArray())
            {
                var severidad = elemento.TryGetProperty("severidad", out var s) ? s.GetString() : null;
                var duracionMs = elemento.TryGetProperty("duracionMs", out var d) && d.TryGetInt32(out var ms) ? ms : 0;

                if (string.Equals(severidad, "Roja", StringComparison.OrdinalIgnoreCase) || duracionMs >= 3000)
                {
                    rojas++;
                }
                else
                {
                    amarillas++;
                }
            }

            return new AlertasFocoResumenDto(amarillas, rojas);
        }
        catch
        {
            return new AlertasFocoResumenDto(0, 0);
        }
    }

    private static List<AlertaFocoDetalleDto> MapearAlertasDetalle(string alertasJson)
    {
        var lista = new List<AlertaFocoDetalleDto>();
        if (string.IsNullOrWhiteSpace(alertasJson)) return lista;

        try
        {
            using var doc = JsonDocument.Parse(alertasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return lista;

            foreach (var elemento in doc.RootElement.EnumerateArray())
            {
                Guid? preguntaId = null;
                if (elemento.TryGetProperty("preguntaId", out var pid) && Guid.TryParse(pid.GetString(), out var g))
                {
                    preguntaId = g;
                }

                var tipoEvento = elemento.TryGetProperty("tipoEvento", out var te) ? te.GetString() ?? "cambio-pantalla" : "cambio-pantalla";
                var duracionMs = elemento.TryGetProperty("duracionMs", out var d) && d.TryGetInt32(out var ms) ? ms : 0;
                var severidad = elemento.TryGetProperty("severidad", out var s) ? s.GetString() ?? (duracionMs >= 3000 ? "Roja" : "Amarillo") : (duracionMs >= 3000 ? "Roja" : "Amarillo");
                
                DateTimeOffset registradoUtc = DateTimeOffset.UtcNow;
                if (elemento.TryGetProperty("registradoUtc", out var reg) && DateTimeOffset.TryParse(reg.GetString(), out var fecha))
                {
                    registradoUtc = fecha;
                }

                lista.Add(new AlertaFocoDetalleDto(preguntaId, tipoEvento, duracionMs, severidad, registradoUtc));
            }
        }
        catch { }

        return lista;
    }

    private static bool VerificarSiRespondioAlgo(string respuestasJson)
    {
        if (string.IsNullOrWhiteSpace(respuestasJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(respuestasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("respuesta", out var r) && !string.IsNullOrWhiteSpace(r.GetString())) return true;
                if (item.TryGetProperty("opcionElegidaId", out var o) && !string.IsNullOrWhiteSpace(o.GetString())) return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static List<RespuestaEntregaDto> MapearRespuestas(
        string respuestasJson,
        EstructuraPreguntasEvaluacion estructura,
        List<CalificacionSugerida> calificaciones)
    {
        var respuestasGuardadas = new Dictionary<Guid, (string? respuesta, string? opcionElegidaId)>();
        if (!string.IsNullOrWhiteSpace(respuestasJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(respuestasJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("preguntaId", out var pid) && Guid.TryParse(pid.GetString(), out var pGuid))
                        {
                            var r = elem.TryGetProperty("respuesta", out var rProp) ? rProp.GetString() : null;
                            var o = elem.TryGetProperty("opcionElegidaId", out var oProp) ? oProp.GetString() : null;
                            respuestasGuardadas[pGuid] = (r, o);
                        }
                    }
                }
            }
            catch { }
        }

        var lista = new List<RespuestaEntregaDto>();
        foreach (var p in estructura.Preguntas)
        {
            respuestasGuardadas.TryGetValue(p.Id, out var resp);
            var respuestaTexto = resp.respuesta;
            var opcionId = resp.opcionElegidaId ?? resp.respuesta;

            string? opcionElegidaTexto = null;
            bool? esCorrecta = null;
            decimal? puntajeObtenido = null;
            decimal? notaSugerida = null;
            string? justificacionIa = null;
            List<DesgloseCriterioDto>? desglose = null;
            string estadoCorreccion;

            if (p.Tipo == "OpcionMultiple")
            {
                if (Guid.TryParse(opcionId, out var opcGuid) && p.Opciones.TryGetValue(opcGuid, out var opcionInfo))
                {
                    opcionElegidaTexto = opcionInfo.Texto;
                    esCorrecta = opcionInfo.EsCorrecta;
                    puntajeObtenido = opcionInfo.EsCorrecta ? p.PuntajeMaximo : 0m;
                    estadoCorreccion = "Automatica";
                }
                else
                {
                    estadoCorreccion = "SinRespuesta";
                }
            }
            else // Abierta
            {
                if (string.IsNullOrWhiteSpace(respuestaTexto))
                {
                    estadoCorreccion = "SinRespuesta";
                }
                else
                {
                    var calif = calificaciones.FirstOrDefault(c => c.PreguntaId == p.Id);
                    if (calif != null)
                    {
                        notaSugerida = calif.NotaSugerida;
                        justificacionIa = calif.Justificacion;
                        estadoCorreccion = "SugeridaIA";
                        if (!string.IsNullOrWhiteSpace(calif.DesgloseJson))
                        {
                            try
                            {
                                desglose = JsonSerializer.Deserialize<List<DesgloseCriterioDto>>(
                                    calif.DesgloseJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        estadoCorreccion = "PendienteIA";
                    }
                }
            }

            lista.Add(new RespuestaEntregaDto(
                p.Id,
                p.Enunciado,
                p.Tipo,
                p.PuntajeMaximo,
                respuestaTexto,
                opcionElegidaTexto,
                esCorrecta,
                puntajeObtenido,
                notaSugerida,
                justificacionIa,
                desglose,
                estadoCorreccion));
        }

        return lista;
    }

    private static EstructuraPreguntasEvaluacion ParsearEstructuraPreguntas(string preguntasJson)
    {
        var lista = new List<PreguntaEvaluacionInfo>();
        if (string.IsNullOrWhiteSpace(preguntasJson))
            return new EstructuraPreguntasEvaluacion(lista);

        try
        {
            using var doc = JsonDocument.Parse(preguntasJson);
            if (doc.RootElement.TryGetProperty("preguntas", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var id))
                    {
                        var enunciado = elem.TryGetProperty("enunciado", out var e) ? e.GetString() ?? "" : "";
                        var tipo = elem.TryGetProperty("tipo", out var t) ? t.GetString() ?? "Abierta" : "Abierta";
                        var puntajeMaximo = elem.TryGetProperty("puntajeMaximo", out var pm) && pm.TryGetDecimal(out var max) ? max : 0m;
                        var opciones = new Dictionary<Guid, (string Texto, bool EsCorrecta)>();

                        if (elem.TryGetProperty("opciones", out var opcs) && opcs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var o in opcs.EnumerateArray())
                            {
                                if (o.TryGetProperty("id", out var oid) && Guid.TryParse(oid.GetString(), out var oGuid))
                                {
                                    var texto = o.TryGetProperty("texto", out var txt) ? txt.GetString() ?? "" : "";
                                    var corr = o.TryGetProperty("esCorrecta", out var c) && c.GetBoolean();
                                    opciones[oGuid] = (texto, corr);
                                }
                            }
                        }

                        lista.Add(new PreguntaEvaluacionInfo(id, enunciado, tipo, puntajeMaximo, opciones));
                    }
                }
            }
        }
        catch { }

        return new EstructuraPreguntasEvaluacion(lista);
    }

    private record PreguntaEvaluacionInfo(
        Guid Id,
        string Enunciado,
        string Tipo,
        decimal PuntajeMaximo,
        Dictionary<Guid, (string Texto, bool EsCorrecta)> Opciones);

    private class EstructuraPreguntasEvaluacion
    {
        public List<PreguntaEvaluacionInfo> Preguntas { get; }
        public bool TienePreguntasMultipleChoice => Preguntas.Any(p => p.Tipo == "OpcionMultiple");
        public decimal PuntajeMaximoMultipleChoice => Preguntas.Where(p => p.Tipo == "OpcionMultiple").Sum(p => p.PuntajeMaximo);
        public bool TienePreguntasAbiertas => Preguntas.Any(p => p.Tipo == "Abierta");
        public int CantidadPreguntasAbiertas => Preguntas.Count(p => p.Tipo == "Abierta");
        public decimal PuntajeMaximoAbiertas => Preguntas.Where(p => p.Tipo == "Abierta").Sum(p => p.PuntajeMaximo);

        public EstructuraPreguntasEvaluacion(List<PreguntaEvaluacionInfo> preguntas)
        {
            Preguntas = preguntas;
        }
    }
}