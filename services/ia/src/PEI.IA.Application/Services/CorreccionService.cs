using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Guardrails;
using PEI.IA.Application.Interfaces;
using PEI.IA.Domain.Entities;

namespace PEI.IA.Application.Services;

/// <summary>
/// Orquestador del flujo de corrección pedagógica asistida por IA.
/// Integra barandas de seguridad pre y post inferencia, el constructor de prompts,
/// el cliente LLM compatible con OpenAI y el ejecutor de herramientas MCP.
/// </summary>
public class CorreccionService : ICorreccionService
{
    private readonly ILlmClient _llmClient;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IToolExecutor _toolExecutor;
    private readonly IBarandaSeguridad _barandaSeguridad;
    private readonly IEvaluacionesClient? _evaluacionesClient;
    private readonly ISolicitudCorreccionRepository? _solicitudRepository;
    private readonly ILogger<CorreccionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IBarandaSeguridad barandaSeguridad,
        IEvaluacionesClient? evaluacionesClient,
        ISolicitudCorreccionRepository? solicitudRepository,
        ILogger<CorreccionService> logger)
    {
        _llmClient = llmClient;
        _promptBuilder = promptBuilder;
        _toolExecutor = toolExecutor;
        _barandaSeguridad = barandaSeguridad;
        _evaluacionesClient = evaluacionesClient;
        _solicitudRepository = solicitudRepository;
        _logger = logger;
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IBarandaSeguridad barandaSeguridad,
        IEvaluacionesClient? evaluacionesClient,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, barandaSeguridad, evaluacionesClient, null, logger)
    {
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IBarandaSeguridad barandaSeguridad,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, barandaSeguridad, null, null, logger)
    {
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IEvaluacionesClient? evaluacionesClient,
        ISolicitudCorreccionRepository? solicitudRepository,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, new BarandaSeguridad(), evaluacionesClient, solicitudRepository, logger)
    {
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IEvaluacionesClient? evaluacionesClient,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, new BarandaSeguridad(), evaluacionesClient, null, logger)
    {
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, new BarandaSeguridad(), null, null, logger)
    {
    }

    public async Task<CorregirRespuestaResponse> CorregirAsync(
        CorregirRespuestaRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando corrección para pregunta: '{Enunciado}' (PuntajeMax: {Max})",
            request.PreguntaEnunciado[..Math.Min(60, request.PreguntaEnunciado.Length)],
            request.PuntajeMaximo);

        var institucionId = request.InstitucionId ?? Guid.Empty;
        var solicitud = SolicitudCorreccion.Crear(
            institucionId: institucionId,
            respuestaTexto: request.RespuestaTexto,
            preguntaEnunciado: request.PreguntaEnunciado,
            rubricaJson: request.RubricaJson,
            puntajeMaximo: request.PuntajeMaximo,
            evaluacionId: request.EvaluacionId,
            preguntaId: request.PreguntaId);

        // ── Baranda Pre-Inferencia: Detección de intento de engaño o inyección de prompt ──
        var evaluacionSeguridad = _barandaSeguridad.EvaluarEntrada(request.RespuestaTexto);
        var esSospechosa = evaluacionSeguridad.EsSospechosa;
        var motivoSospecha = evaluacionSeguridad.Motivo;

        if (esSospechosa)
        {
            _logger.LogWarning(
                "[Barandas IA] Se detectó posible intento de engaño/inyección en la respuesta del alumno. Motivo: {Motivo}",
                motivoSospecha);
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(
            request.PreguntaEnunciado,
            request.RubricaJson,
            request.PuntajeMaximo,
            request.RespuestaTexto);

        IReadOnlyList<LlmToolDefinition>? tools = null;
        try
        {
            tools = await _toolExecutor.ListarToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron listar las herramientas de MCP. Continuando sin tools.");
        }

        // ── Inferencia con reintento ante formato inválido (máximo 1 reintento) ──
        LlmCompletionResult? llmResult = null;
        DatosEvaluacion? datosValidados = null;

        for (int intento = 1; intento <= 2; intento++)
        {
            try
            {
                llmResult = await _llmClient.CompletarAsync(systemPrompt, userPrompt, tools, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invocar LLM externo en intento {Intento}.", intento);
            }

            if (llmResult == null)
            {
                break; // Fallo de red/disponibilidad del proveedor
            }

            // Extraer y parsear datos del resultado
            var datos = ExtraerDatosEvaluacion(llmResult, request.PuntajeMaximo);
            if (datos != null)
            {
                // Validación estricta de salida en código
                var validacion = _barandaSeguridad.ValidarSalida(
                    datos.NotaSugerida,
                    request.PuntajeMaximo,
                    datos.Justificacion,
                    datos.Desglose);

                if (validacion.EsValido)
                {
                    datosValidados = datos;
                    break; // Éxito en validación
                }

                _logger.LogWarning(
                    "[Barandas IA] Intento {Intento}: Formato o rangos inválidos devueltos por el LLM. Detalle: {Error}",
                    intento, validacion.ErrorValidacion);
            }
            else
            {
                _logger.LogWarning(
                    "[Barandas IA] Intento {Intento}: No se pudo parsear el JSON ni ejecutar la tool correctamente.",
                    intento);
            }

            if (intento == 1)
            {
                _logger.LogInformation("[Barandas IA] Reintentando llamada al LLM (intento 2 de 2)...");
            }
        }

        // ── Caso A: Fallo total de disponibilidad del LLM ──
        if (llmResult is null)
        {
            var fallback = GenerarFallback(request, "El proveedor LLM no estuvo disponible o no devolvió respuesta.");
            fallback.SolicitudId = solicitud.Id;

            solicitud.RegistrarError("Proveedor LLM no disponible o fallo de conexion.");
            await GuardarSolicitudSeguraAsync(solicitud, cancellationToken);
            return fallback;
        }

        // ── Caso B: El formato continuó siendo inválido luego del reintento ──
        if (datosValidados is null)
        {
            _logger.LogWarning("[Barandas IA] El formato del LLM es inválido tras reintentar. Derivando a revisión manual.");
            var respuestaRevision = GenerarRespuestaRevisionManual(
                solicitud.Id,
                request,
                llmResult,
                "El modelo de IA devolvió un formato o estructura de puntaje inválida después de reintentar.",
                esSospechaEngano: esSospechosa);

            solicitud.MarcarParaRevisionDocente(
                motivo: respuestaRevision.MotivoRevision ?? "Formato inválido tras reintento",
                esSospechaEngano: esSospechosa,
                notaSugerida: 0m,
                justificacion: respuestaRevision.Justificacion,
                desglosePorCriterioJson: JsonSerializer.Serialize(respuestaRevision.DesglosePorCriterio, JsonOptions),
                proveedor: llmResult.Proveedor,
                modelo: llmResult.Modelo,
                tokensEntrada: llmResult.TokensEntrada,
                tokensSalida: llmResult.TokensSalida,
                latenciaMs: llmResult.LatenciaMs);

            await GuardarSolicitudSeguraAsync(solicitud, cancellationToken);
            await NotificarEvaluacionesSiAplicaAsync(request, respuestaRevision, cancellationToken);
            return respuestaRevision;
        }

        // ── Caso C: Salida válida del LLM ──
        var respuesta = ConstruirRespuesta(datosValidados, llmResult);
        respuesta.SolicitudId = solicitud.Id;

        if (esSospechosa)
        {
            respuesta.RequiereRevisionDocente = true;
            respuesta.SospechaEngano = true;
            respuesta.MotivoRevision = motivoSospecha ?? "Intento de manipulación de instrucciones detectado en la respuesta.";
            respuesta.Justificacion = $"[Alerta Docente: {respuesta.MotivoRevision}] {respuesta.Justificacion}";

            solicitud.MarcarParaRevisionDocente(
                motivo: respuesta.MotivoRevision,
                esSospechaEngano: true,
                notaSugerida: respuesta.NotaSugerida,
                justificacion: respuesta.Justificacion,
                desglosePorCriterioJson: JsonSerializer.Serialize(respuesta.DesglosePorCriterio, JsonOptions),
                proveedor: respuesta.Proveedor,
                modelo: respuesta.Modelo,
                tokensEntrada: respuesta.TokensEntrada,
                tokensSalida: respuesta.TokensSalida,
                latenciaMs: respuesta.LatenciaMs);
        }
        else
        {
            solicitud.RegistrarResultado(
                notaSugerida: respuesta.NotaSugerida,
                justificacion: respuesta.Justificacion,
                desglosePorCriterioJson: JsonSerializer.Serialize(respuesta.DesglosePorCriterio, JsonOptions),
                proveedor: respuesta.Proveedor,
                modelo: respuesta.Modelo,
                tokensEntrada: respuesta.TokensEntrada,
                tokensSalida: respuesta.TokensSalida,
                latenciaMs: respuesta.LatenciaMs,
                costoEstimadoUsd: 0m);
        }

        await GuardarSolicitudSeguraAsync(solicitud, cancellationToken);
        await NotificarEvaluacionesSiAplicaAsync(request, respuesta, cancellationToken);

        return respuesta;
    }

    private DatosEvaluacion? ExtraerDatosEvaluacion(LlmCompletionResult llmResult, decimal puntajeMaximo)
    {
        if (llmResult.ToolCalls is { Count: > 0 })
        {
            var targetTool = llmResult.ToolCalls.FirstOrDefault(t =>
                t.NombreTool.Equals("RegistrarCalificacionSugerida", StringComparison.OrdinalIgnoreCase))
                ?? llmResult.ToolCalls[0];

            return ParsearResultadoDeJson(targetTool.ArgumentosJson, puntajeMaximo);
        }

        if (!string.IsNullOrWhiteSpace(llmResult.Contenido))
        {
            return ParsearResultadoDeJson(llmResult.Contenido, puntajeMaximo);
        }

        return null;
    }

    private static DatosEvaluacion? ParsearResultadoDeJson(string json, decimal puntajeMaximo)
    {
        try
        {
            var limpio = json.Trim();
            if (limpio.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                limpio = limpio[7..];
            }
            else if (limpio.StartsWith("```"))
            {
                limpio = limpio[3..];
            }
            if (limpio.EndsWith("```"))
            {
                limpio = limpio[..^3];
            }
            limpio = limpio.Trim();

            var modelo = JsonSerializer.Deserialize<ModeloJsonEvaluacion>(limpio, JsonOptions);
            if (modelo != null)
            {
                var desglose = modelo.DesglosePorCriterio?.Select(d => new DesgloseCriterio
                {
                    Criterio = d.Criterio ?? "Criterio",
                    PuntajeMaximo = d.PuntajeMaximo,
                    PuntajeOtorgado = d.PuntajeOtorgado,
                    Comentario = d.Comentario ?? string.Empty
                }).ToList() ?? new List<DesgloseCriterio>();

                return new DatosEvaluacion(
                    modelo.NotaSugerida,
                    string.IsNullOrWhiteSpace(modelo.Justificacion) ? "Evaluación completada." : modelo.Justificacion,
                    desglose
                );
            }
        }
        catch
        {
            // Error al parsear JSON
        }

        return null;
    }

    private static CorregirRespuestaResponse ConstruirRespuesta(DatosEvaluacion datos, LlmCompletionResult llmResult)
    {
        return new CorregirRespuestaResponse
        {
            SolicitudId = Guid.NewGuid(),
            NotaSugerida = datos.NotaSugerida,
            Justificacion = datos.Justificacion,
            DesglosePorCriterio = datos.Desglose,
            Proveedor = llmResult.Proveedor,
            Modelo = llmResult.Modelo,
            TokensEntrada = llmResult.TokensEntrada,
            TokensSalida = llmResult.TokensSalida,
            LatenciaMs = llmResult.LatenciaMs
        };
    }

    private static CorregirRespuestaResponse GenerarRespuestaRevisionManual(
        Guid solicitudId,
        CorregirRespuestaRequest request,
        LlmCompletionResult llmResult,
        string motivo,
        bool esSospechaEngano)
    {
        return new CorregirRespuestaResponse
        {
            SolicitudId = solicitudId,
            NotaSugerida = 0m,
            Justificacion = $"[Revisión Manual Docente Requerida] {motivo}",
            DesglosePorCriterio = new List<DesgloseCriterio>
            {
                new()
                {
                    Criterio = "Revisión Manual",
                    PuntajeMaximo = request.PuntajeMaximo,
                    PuntajeOtorgado = 0m,
                    Comentario = motivo
                }
            },
            Proveedor = llmResult.Proveedor,
            Modelo = llmResult.Modelo,
            TokensEntrada = llmResult.TokensEntrada,
            TokensSalida = llmResult.TokensSalida,
            LatenciaMs = llmResult.LatenciaMs,
            RequiereRevisionDocente = true,
            SospechaEngano = esSospechaEngano,
            MotivoRevision = motivo
        };
    }

    private static CorregirRespuestaResponse GenerarFallback(CorregirRespuestaRequest request, string motivo)
    {
        var notaEstimada = Math.Round(request.PuntajeMaximo * 0.7m, 1);
        return new CorregirRespuestaResponse
        {
            SolicitudId = Guid.NewGuid(),
            NotaSugerida = notaEstimada,
            Justificacion = $"[Fallback] {motivo} Se generó una sugerencia preliminar basada en el 70% del puntaje máximo.",
            DesglosePorCriterio = new List<DesgloseCriterio>
            {
                new()
                {
                    Criterio = "Evaluación general",
                    PuntajeMaximo = request.PuntajeMaximo,
                    PuntajeOtorgado = notaEstimada,
                    Comentario = "Pendiente de reintento con el proveedor de IA."
                }
            },
            Proveedor = "Fallback-Local",
            Modelo = "heuristic-v1",
            TokensEntrada = 0,
            TokensSalida = 0,
            LatenciaMs = 0
        };
    }

    private async Task GuardarSolicitudSeguraAsync(SolicitudCorreccion solicitud, CancellationToken ct)
    {
        if (_solicitudRepository == null) return;
        try
        {
            await _solicitudRepository.GuardarAsync(solicitud, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar la SolicitudCorreccion en el repositorio.");
        }
    }

    private async Task NotificarEvaluacionesSiAplicaAsync(
        CorregirRespuestaRequest request,
        CorregirRespuestaResponse respuesta,
        CancellationToken ct)
    {
        if (!request.EntregaId.HasValue || _evaluacionesClient == null) return;

        try
        {
            var desgloseJson = JsonSerializer.Serialize(respuesta.DesglosePorCriterio, JsonOptions);
            await _evaluacionesClient.RegistrarSugerenciaAsync(
                entregaId: request.EntregaId.Value,
                notaSugerida: respuesta.NotaSugerida,
                justificacion: respuesta.Justificacion,
                desgloseJson: desgloseJson,
                preguntaId: request.PreguntaId,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Excepción al registrar sugerencia en Evaluaciones para EntregaId={EntregaId}.",
                request.EntregaId.Value);
        }
    }

    private record DatosEvaluacion(decimal NotaSugerida, string Justificacion, List<DesgloseCriterio> Desglose);

    private class ModeloJsonEvaluacion
    {
        public decimal NotaSugerida { get; set; }
        public string? Justificacion { get; set; }
        public List<ModeloJsonDesglose>? DesglosePorCriterio { get; set; }
    }

    private class ModeloJsonDesglose
    {
        public string? Criterio { get; set; }
        public decimal PuntajeMaximo { get; set; }
        public decimal PuntajeOtorgado { get; set; }
        public string? Comentario { get; set; }
    }
}
