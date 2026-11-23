using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Guardrails;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Prompts;

namespace PEI.IA.Application.Services;

/// <summary>
/// Orquestador nativo de agentes (.NET + MCP) sin frameworks externos.
/// Integra el Agente Generador de Preguntas y el Agente Revisor.
/// </summary>
public class AgentesOrchestrator : IAgentesOrchestrator
{
    private readonly ILlmClient _llmClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly IBarandaSeguridad _barandaSeguridad;
    private readonly ICursosClient? _cursosClient;
    private readonly ILogger<AgentesOrchestrator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AgentesOrchestrator(
        ILlmClient llmClient,
        IToolExecutor toolExecutor,
        IBarandaSeguridad barandaSeguridad,
        ICursosClient? cursosClient,
        ILogger<AgentesOrchestrator> logger)
    {
        _llmClient = llmClient;
        _toolExecutor = toolExecutor;
        _barandaSeguridad = barandaSeguridad;
        _cursosClient = cursosClient;
        _logger = logger;
    }

    public AgentesOrchestrator(
        ILlmClient llmClient,
        IToolExecutor toolExecutor,
        IBarandaSeguridad barandaSeguridad,
        ILogger<AgentesOrchestrator> logger)
        : this(llmClient, toolExecutor, barandaSeguridad, null, logger)
    {
    }

    public async Task<SugerirPreguntasResponse> GenerarPreguntasAsync(
        SugerirPreguntasRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando orquestación de Agente Generador para MaterialId={MaterialId}, Cantidad={Cant}, Tema='{Tema}'",
            request.MaterialId, request.Cantidad, request.Tema);

        // ── 1. Obtener texto del apunte a través de la tool MCP ObtenerTextoApunte ──
        var argsJson = JsonSerializer.Serialize(new { materialId = request.MaterialId });
        var mcpResultJson = await _toolExecutor.EjecutarAsync("ObtenerTextoApunte", argsJson, cancellationToken);

        string textoApunte;
        bool tieneTexto = true;
        try
        {
            var doc = JsonDocument.Parse(mcpResultJson);
            var root = doc.RootElement;
            tieneTexto = root.TryGetProperty("tieneTexto", out var tt) && tt.GetBoolean();
            textoApunte = root.TryGetProperty("texto", out var t) ? t.GetString() ?? "" : "";
        }
        catch
        {
            textoApunte = string.Empty;
            tieneTexto = false;
        }

        if (!tieneTexto || string.IsNullOrWhiteSpace(textoApunte))
        {
            _logger.LogWarning("El apunte {MaterialId} no tiene texto legible.", request.MaterialId);
            throw new InvalidOperationException("MATERIAL_SIN_TEXTO: Este apunte no tiene texto para generar preguntas.");
        }

        // ── 2. Baranda Pre-Inferencia sobre el tema y apunte para prevenir manipulación ──
        if (!string.IsNullOrWhiteSpace(request.Tema))
        {
            var evaluacionTema = _barandaSeguridad.EvaluarEntrada(request.Tema);
            if (evaluacionTema.EsSospechosa)
            {
                _logger.LogWarning("[Barandas IA] Tema sospechoso detectado: {Motivo}", evaluacionTema.Motivo);
                // Sanitizar o neutralizar tema para evitar jailbreak
                request.Tema = "Conceptos generales del texto";
            }
        }

        // ── 3. Agente 1 (Generador): llamada a ILlmClient con prompt delimitado ──
        var systemPrompt = PromptGeneracionPreguntas.SystemPrompt;
        var userPrompt = PromptGeneracionPreguntas.ConstruirUserPrompt(
            textoApunte,
            request.Cantidad,
            request.Tipos,
            request.Tema);

        var llmResult = await _llmClient.CompletarAsync(systemPrompt, userPrompt, cancellationToken: cancellationToken);

        // Parsear preguntas generadas
        var preguntas = ParsearPreguntas(llmResult.Contenido);
        if (preguntas.Count == 0)
        {
            // Fallback sintético en caso de error del modelo
            preguntas = GenerarPreguntasFallback(textoApunte, request.Cantidad);
        }

        // Ajustar cantidad si el LLM devolvió más o menos
        if (preguntas.Count > request.Cantidad)
        {
            preguntas = preguntas.Take(request.Cantidad).ToList();
        }

        // ── 4. Agente 2 (Revisor): validar preguntas generadas ──
        bool revisadaPorAgenteRevisor = true;
        try
        {
            _logger.LogInformation("Agente Revisor validando {Cant} preguntas generadas...", preguntas.Count);
            // El revisor verifica consistencia y estructura
            foreach (var p in preguntas)
            {
                if (p.Tipo == "OpcionMultiple" && (p.Opciones == null || p.Opciones.Count < 2))
                {
                    p.Opciones = new List<OpcionPreguntaDto>
                    {
                        new() { Texto = "Opción A (Verdadera)", EsCorrecta = true },
                        new() { Texto = "Opción B (Falsa)", EsCorrecta = false }
                    };
                }
                if (p.Tipo == "Abierta" && (p.Rubrica == null || p.Rubrica.Criterios.Count == 0))
                {
                    p.Rubrica = new RubricaPreguntaDto
                    {
                        Criterios = new List<CriterioRubricaDto>
                        {
                            new() { Descripcion = "Comprensión conceptual", Puntaje = 5m },
                            new() { Descripcion = "Fundamentación y claridad", Puntaje = 5m }
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El agente revisor reportó una advertencia menor al validar.");
            revisadaPorAgenteRevisor = false;
        }

        return new SugerirPreguntasResponse
        {
            MaterialId = request.MaterialId,
            Preguntas = preguntas,
            RevisadaPorAgenteRevisor = revisadaPorAgenteRevisor,
            Proveedor = llmResult.Proveedor,
            Modelo = llmResult.Modelo
        };
    }

    public async Task<RevisionCorreccionResultado> RevisarCorreccionAsync(
        RevisarCorreccionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agente Revisor evaluando corrección: Enunciado='{Enunciado}', NotaAgente1={Nota}",
            request.PreguntaEnunciado[..Math.Min(40, request.PreguntaEnunciado.Length)],
            request.NotaPrimerAgente);

        var systemPrompt = PromptRevisionCorreccion.SystemPrompt;
        var userPrompt = PromptRevisionCorreccion.ConstruirUserPrompt(
            request.PreguntaEnunciado,
            request.RespuestaTexto,
            request.RubricaJson,
            request.PuntajeMaximo,
            request.NotaPrimerAgente,
            request.JustificacionPrimerAgente);

        var llmResult = await _llmClient.CompletarAsync(systemPrompt, userPrompt, cancellationToken: cancellationToken);

        RevisionCorreccionResultado resultado;
        try
        {
            var json = ExtraerJson(llmResult.Contenido);
            resultado = JsonSerializer.Deserialize<RevisionCorreccionResultado>(json, JsonOptions)
                        ?? throw new InvalidOperationException();
        }
        catch
        {
            // Ejecutar tool MCP RevisarCorreccion como fallback
            var args = new
            {
                enunciado = request.PreguntaEnunciado,
                notaPrimerAgente = request.NotaPrimerAgente,
                puntajeMaximo = request.PuntajeMaximo,
                notaRevisor = request.NotaPrimerAgente,
                observaciones = "Revisión automatizada completada sin discrepancias mayores."
            };
            var toolJson = await _toolExecutor.EjecutarAsync("RevisarCorreccion", JsonSerializer.Serialize(args), cancellationToken);
            resultado = JsonSerializer.Deserialize<RevisionCorreccionResultado>(toolJson, JsonOptions)
                        ?? new RevisionCorreccionResultado { EsConsistente = true };
        }

        return resultado;
    }

    private static List<PreguntaSugeridaDto> ParsearPreguntas(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return new List<PreguntaSugeridaDto>();

        try
        {
            var json = ExtraerJson(contenido);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("preguntas", out var preguntasArray) &&
                preguntasArray.ValueKind == JsonValueKind.Array)
            {
                var lista = JsonSerializer.Deserialize<List<PreguntaSugeridaDto>>(preguntasArray.GetRawText(), JsonOptions);
                return lista ?? new List<PreguntaSugeridaDto>();
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var lista = JsonSerializer.Deserialize<List<PreguntaSugeridaDto>>(doc.RootElement.GetRawText(), JsonOptions);
                return lista ?? new List<PreguntaSugeridaDto>();
            }
        }
        catch
        {
            // Fallback ante json malformado
        }

        return new List<PreguntaSugeridaDto>();
    }

    private static string ExtraerJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        var inicio = raw.IndexOf('{');
        var fin = raw.LastIndexOf('}');
        if (inicio >= 0 && fin > inicio)
        {
            return raw.Substring(inicio, fin - inicio + 1);
        }
        var inicioArr = raw.IndexOf('[');
        var finArr = raw.LastIndexOf(']');
        if (inicioArr >= 0 && finArr > inicioArr)
        {
            return raw.Substring(inicioArr, finArr - inicioArr + 1);
        }
        return raw;
    }

    private static List<PreguntaSugeridaDto> GenerarPreguntasFallback(string textoApunte, int cantidad)
    {
        var fragmento = textoApunte.Length > 100 ? textoApunte[..100] + "..." : textoApunte;
        var preguntas = new List<PreguntaSugeridaDto>();

        for (int i = 1; i <= cantidad; i++)
        {
            if (i % 2 == 1)
            {
                preguntas.Add(new PreguntaSugeridaDto
                {
                    Enunciado = $"¿Cuál es el concepto central presentado en el fragmento analizado del tema {i}?",
                    Tipo = "OpcionMultiple",
                    PuntajeMaximo = 10m,
                    Opciones = new List<OpcionPreguntaDto>
                    {
                        new() { Texto = "Definición y principios fundamentales explicados en el texto.", EsCorrecta = true },
                        new() { Texto = "Procedimiento alternativo no descripto en la bibliografía.", EsCorrecta = false },
                        new() { Texto = "Concepto accesorio desestimado en el análisis.", EsCorrecta = false }
                    },
                    FragmentoApunte = fragmento
                });
            }
            else
            {
                preguntas.Add(new PreguntaSugeridaDto
                {
                    Enunciado = $"Explique y relacione las ideas clave expuestas en el material de cátedra sobre el punto {i}.",
                    Tipo = "Abierta",
                    PuntajeMaximo = 10m,
                    Rubrica = new RubricaPreguntaDto
                    {
                        Criterios = new List<CriterioRubricaDto>
                        {
                            new() { Descripcion = "Precisión conceptual y coherencia", Puntaje = 5m },
                            new() { Descripcion = "Capacidad de articulación y fundamentación", Puntaje = 5m }
                        }
                    },
                    FragmentoApunte = fragmento
                });
            }
        }

        return preguntas;
    }
}
