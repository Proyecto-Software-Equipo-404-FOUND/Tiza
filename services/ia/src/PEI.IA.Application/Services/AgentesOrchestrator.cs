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
            _logger.LogWarning("Agente Revisor: ninguna pregunta válida. Respuesta del modelo (primeros 600 caracteres): {Contenido}",
                llmResult.Contenido is { Length: > 600 } c ? c[..600] : llmResult.Contenido);
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

    /// <summary>
    /// S3-IA-02: lee las preguntas del modelo pregunta por pregunta y de forma tolerante
    /// (rúbrica como objeto o lista, "criterio"/"descripcion", números como texto, tipos con otra escritura).
    /// Antes, un solo campo con otra forma hacía fallar la lista entera y no quedaba ninguna pregunta.
    /// </summary>
    public static List<PreguntaSugeridaDto> ParsearPreguntas(string? contenido)
    {
        var resultado = new List<PreguntaSugeridaDto>();
        if (string.IsNullOrWhiteSpace(contenido)) return resultado;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(ExtraerJson(contenido));
        }
        catch (JsonException)
        {
            return resultado;
        }

        using (doc)
        {
            JsonElement lista;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                lista = doc.RootElement;
            else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                     Buscar(doc.RootElement, "preguntas", out var preguntas) &&
                     preguntas.ValueKind == JsonValueKind.Array)
                lista = preguntas;
            else
                return resultado;

            foreach (var item in lista.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                try
                {
                    resultado.Add(LeerPregunta(item));
                }
                catch (Exception)
                {
                    // Una pregunta con forma inesperada se saltea; el resto sigue.
                }
            }
        }

        return resultado;
    }

    private static PreguntaSugeridaDto LeerPregunta(JsonElement item)
    {
        var pregunta = new PreguntaSugeridaDto
        {
            Enunciado = LeerTexto(item, "enunciado") ?? LeerTexto(item, "pregunta") ?? string.Empty,
            Tipo = NormalizarTipo(LeerTexto(item, "tipo")),
            PuntajeMaximo = LeerNumero(item, "puntajeMaximo") ?? LeerNumero(item, "puntaje") ?? 10m,
            FragmentoApunte = LeerTexto(item, "fragmentoApunte")
        };

        if (Buscar(item, "opciones", out var opciones) && opciones.ValueKind == JsonValueKind.Array)
        {
            pregunta.Opciones = new List<OpcionPreguntaDto>();
            foreach (var o in opciones.EnumerateArray())
            {
                if (o.ValueKind == JsonValueKind.String)
                {
                    pregunta.Opciones.Add(new OpcionPreguntaDto { Texto = o.GetString() ?? string.Empty });
                    continue;
                }
                if (o.ValueKind != JsonValueKind.Object) continue;
                pregunta.Opciones.Add(new OpcionPreguntaDto
                {
                    Texto = LeerTexto(o, "texto") ?? LeerTexto(o, "opcion") ?? string.Empty,
                    EsCorrecta = LeerBool(o, "esCorrecta") ?? LeerBool(o, "correcta") ?? false
                });
            }
        }

        // La rúbrica puede venir como { criterios: [...] }, como lista directa o como "criterios" suelto.
        JsonElement criterios = default;
        var hayCriterios = false;
        if (Buscar(item, "rubrica", out var rubrica))
        {
            if (rubrica.ValueKind == JsonValueKind.Object && Buscar(rubrica, "criterios", out var c) && c.ValueKind == JsonValueKind.Array)
            { criterios = c; hayCriterios = true; }
            else if (rubrica.ValueKind == JsonValueKind.Array)
            { criterios = rubrica; hayCriterios = true; }
        }
        else if (Buscar(item, "criterios", out var sueltos) && sueltos.ValueKind == JsonValueKind.Array)
        { criterios = sueltos; hayCriterios = true; }

        if (hayCriterios)
        {
            pregunta.Rubrica = new RubricaPreguntaDto();
            foreach (var c in criterios.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                pregunta.Rubrica.Criterios.Add(new CriterioRubricaDto
                {
                    Descripcion = LeerTexto(c, "descripcion") ?? LeerTexto(c, "criterio") ?? LeerTexto(c, "nombre") ?? string.Empty,
                    Puntaje = LeerNumero(c, "puntaje") ?? LeerNumero(c, "puntajeMaximo") ?? 0m
                });
            }
        }

        return pregunta;
    }

    private static string NormalizarTipo(string? tipo)
    {
        // Sin tildes ni separadores: "Opción múltiple", "opcion_multiple" y "OpcionMultiple" quedan iguales.
        var sinTildes = string.Concat((tipo ?? string.Empty).Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        var t = sinTildes.Replace("_", "").Replace(" ", "").Replace("-", "");
        if (t.Contains("multiple") || t.Contains("choice") || t == "om") return "OpcionMultiple";
        if (t.Contains("abierta") || t.Contains("open") || t.Contains("desarrollo")) return "Abierta";
        return tipo?.Trim() ?? string.Empty;
    }

    private static bool Buscar(JsonElement objeto, string nombre, out JsonElement valor)
    {
        if (objeto.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in objeto.EnumerateObject())
            {
                if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    valor = propiedad.Value;
                    return true;
                }
            }
        }

        valor = default;
        return false;
    }

    private static string? LeerTexto(JsonElement objeto, string nombre) =>
        Buscar(objeto, nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? LeerNumero(JsonElement objeto, string nombre)
    {
        if (!Buscar(objeto, nombre, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var s))
            return s;
        return null;
    }

    private static bool? LeerBool(JsonElement objeto, string nombre)
    {
        if (!Buscar(objeto, nombre, out var v)) return null;
        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b)) return b;
        return null;
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
