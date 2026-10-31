using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Application.Services;

/// <summary>
/// Orquestador del flujo de corrección pedagógica asistida por IA.
/// Integra el constructor de prompts, el cliente LLM compatible con OpenAI y el ejecutor de herramientas MCP.
/// </summary>
public class CorreccionService : ICorreccionService
{
    private readonly ILlmClient _llmClient;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IToolExecutor _toolExecutor;
    private readonly IEvaluacionesClient? _evaluacionesClient;
    private readonly ILogger<CorreccionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        IEvaluacionesClient? evaluacionesClient,
        ILogger<CorreccionService> logger)
    {
        _llmClient = llmClient;
        _promptBuilder = promptBuilder;
        _toolExecutor = toolExecutor;
        _evaluacionesClient = evaluacionesClient;
        _logger = logger;
    }

    public CorreccionService(
        ILlmClient llmClient,
        IPromptBuilder promptBuilder,
        IToolExecutor toolExecutor,
        ILogger<CorreccionService> logger)
        : this(llmClient, promptBuilder, toolExecutor, null, logger)
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

        LlmCompletionResult? llmResult = null;
        try
        {
            llmResult = await _llmClient.CompletarAsync(systemPrompt, userPrompt, tools, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invocar LLM externo. Se activará modo fallback.");
        }

        // Si el LLM no respondió o falló la llamada, devolvemos fallback estructurado
        if (llmResult is null)
        {
            return GenerarFallback(request, "El proveedor LLM no estuvo disponible o no devolvió respuesta.");
        }

        CorregirRespuestaResponse respuesta;

        // ── Caso 1: El LLM invocó una tool (Tool Calling de MCP) ──
        if (llmResult.ToolCalls is { Count: > 0 })
        {
            var targetTool = llmResult.ToolCalls.FirstOrDefault(t =>
                t.NombreTool.Equals("RegistrarCalificacionSugerida", StringComparison.OrdinalIgnoreCase))
                ?? llmResult.ToolCalls[0];

            _logger.LogInformation("LLM seleccionó tool '{Tool}'. Ejecutando en IToolExecutor...", targetTool.NombreTool);

            try
            {
                await _toolExecutor.EjecutarAsync(targetTool.NombreTool, targetTool.ArgumentosJson, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al ejecutar tool '{Tool}'", targetTool.NombreTool);
            }

            // Parsear nota y justificación de los argumentos de la tool
            var datos = ParsearResultadoDeJson(targetTool.ArgumentosJson, request.PuntajeMaximo);

            respuesta = ConstruirRespuesta(datos, llmResult);
        }
        // ── Caso 2: El LLM respondió con texto / JSON directo ──
        else if (!string.IsNullOrWhiteSpace(llmResult.Contenido))
        {
            var datos = ParsearResultadoDeJson(llmResult.Contenido, request.PuntajeMaximo);
            respuesta = ConstruirRespuesta(datos, llmResult);
        }
        else
        {
            return GenerarFallback(request, "El LLM no invocó ninguna tool ni retornó contenido válido.");
        }

        // ── Integración S2-IA-01 / EV-06: Persistir calificación sugerida en Evaluaciones ──
        if (request.EntregaId.HasValue && _evaluacionesClient != null)
        {
            try
            {
                var desgloseJson = JsonSerializer.Serialize(respuesta.DesglosePorCriterio, JsonOptions);
                var registrado = await _evaluacionesClient.RegistrarSugerenciaAsync(
                    entregaId: request.EntregaId.Value,
                    notaSugerida: respuesta.NotaSugerida,
                    justificacion: respuesta.Justificacion,
                    desgloseJson: desgloseJson,
                    ct: cancellationToken);

                if (!registrado)
                {
                    _logger.LogWarning(
                        "No se pudo persistir la sugerencia en Evaluaciones para EntregaId={EntregaId}. La respuesta al llamador es igualmente válida.",
                        request.EntregaId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Excepción al registrar sugerencia en Evaluaciones para EntregaId={EntregaId}.",
                    request.EntregaId.Value);
            }
        }

        return respuesta;
    }

    private static DatosEvaluacion ParsearResultadoDeJson(string json, decimal puntajeMaximo)
    {
        try
        {
            // Limpiar posibles bloques markdown ```json ... ```
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
                var notaClampeada = Math.Clamp(modelo.NotaSugerida, 0m, puntajeMaximo);
                return new DatosEvaluacion(
                    notaClampeada,
                    string.IsNullOrWhiteSpace(modelo.Justificacion) ? "Evaluación completada." : modelo.Justificacion,
                    modelo.DesglosePorCriterio?.Select(d => new DesgloseCriterio
                    {
                        Criterio = d.Criterio ?? "Criterio",
                        PuntajeMaximo = d.PuntajeMaximo,
                        PuntajeOtorgado = d.PuntajeOtorgado,
                        Comentario = d.Comentario ?? string.Empty
                    }).ToList() ?? new List<DesgloseCriterio>()
                );
            }
        }
        catch
        {
            // Si el parsing JSON falló, devolvemos valores por defecto
        }

        return new DatosEvaluacion(
            Math.Round(puntajeMaximo * 0.7m, 1),
            json.Length > 300 ? json[..300] + "..." : json,
            new List<DesgloseCriterio>()
        );
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
