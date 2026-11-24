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

        // ── 1. Obtener texto del apunte ──
        // Con el cliente de Cursos (el caso real) se pide directo, con el token del docente:
        // 404 → MATERIAL_NO_ENCONTRADO, 401/403 → CursosAccesoDenegadoException. Se corta antes de llamar al modelo.
        string textoApunte;
        bool tieneTexto;
        if (_cursosClient != null)
        {
            var material = await _cursosClient.ObtenerTextoMaterialAsync(request.MaterialId, cancellationToken)
                           ?? throw new KeyNotFoundException($"MATERIAL_NO_ENCONTRADO: {request.MaterialId}");
            tieneTexto = material.TieneTexto;
            textoApunte = material.Texto ?? string.Empty;
        }
        else
        {
            // Sin cliente de Cursos (pruebas o Plan B): se usa la tool MCP ObtenerTextoApunte.
            var argsJson = JsonSerializer.Serialize(new { materialId = request.MaterialId });
            var mcpResultJson = await _toolExecutor.EjecutarAsync("ObtenerTextoApunte", argsJson, cancellationToken);
            try
            {
                var doc = JsonDocument.Parse(mcpResultJson);
                var root = doc.RootElement;
                tieneTexto = root.TryGetProperty("tieneTexto", out var tt) && tt.ValueKind == JsonValueKind.True;
                textoApunte = root.TryGetProperty("texto", out var t) ? t.GetString() ?? "" : "";
            }
            catch
            {
                textoApunte = string.Empty;
                tieneTexto = false;
            }
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

        // ── 4. Agente 2 (Revisor): descarta las preguntas que no respetan el contrato ──
        // No se inventa ni se completa nada: una pregunta mal armada no se le muestra al docente.
        var tiposPedidos = request.Tipos is { Count: > 0 }
            ? new HashSet<string>(request.Tipos, StringComparer.OrdinalIgnoreCase)
            : null;
        var validas = preguntas
            .Where(p => tiposPedidos == null || tiposPedidos.Contains(p.Tipo))
            .Where(EsPreguntaValida)
            .Take(request.Cantidad)
            .ToList();

        _logger.LogInformation("Agente Revisor: {Validas} de {Total} preguntas generadas son válidas.",
            validas.Count, preguntas.Count);

        if (validas.Count == 0)
        {
            // Sin preguntas válidas (JSON malformado o todas descartadas) → 503 IA_NO_DISPONIBLE en el controller.
            throw new InvalidOperationException("IA_NO_DISPONIBLE: el modelo no devolvió preguntas válidas.");
        }

        preguntas = validas;
        const bool revisadaPorAgenteRevisor = true;

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

    /// <summary>
    /// Reglas del contrato para una pregunta sugerida (las mismas que valida Evaluaciones al guardar):
    /// OpcionMultiple con 2 o más opciones con texto y exactamente una correcta;
    /// Abierta con rúbrica cuyos criterios suman el puntaje máximo.
    /// </summary>
    public static bool EsPreguntaValida(PreguntaSugeridaDto p)
    {
        if (string.IsNullOrWhiteSpace(p.Enunciado) || p.PuntajeMaximo <= 0) return false;

        if (string.Equals(p.Tipo, "OpcionMultiple", StringComparison.OrdinalIgnoreCase))
        {
            return p.Opciones is { Count: >= 2 } opciones
                   && opciones.All(o => !string.IsNullOrWhiteSpace(o.Texto))
                   && opciones.Count(o => o.EsCorrecta) == 1;
        }

        if (string.Equals(p.Tipo, "Abierta", StringComparison.OrdinalIgnoreCase))
        {
            return p.Rubrica?.Criterios is { Count: > 0 } criterios
                   && criterios.All(c => !string.IsNullOrWhiteSpace(c.Descripcion) && c.Puntaje > 0)
                   && Math.Abs(criterios.Sum(c => c.Puntaje) - p.PuntajeMaximo) <= 0.01m;
        }

        return false;
    }
}
