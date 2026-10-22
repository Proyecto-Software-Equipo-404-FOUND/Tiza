namespace PEI.IA.Application.Interfaces;

/// <summary>
/// Abstracción del cliente LLM compatible con la API de OpenAI.
/// Permite intercambiar proveedores (Groq, OpenRouter, Local) sin cambiar la lógica de negocio.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Envía un prompt al modelo de lenguaje y devuelve la respuesta.
    /// </summary>
    /// <param name="systemPrompt">Instrucciones del sistema (rol, reglas, formato de salida).</param>
    /// <param name="userPrompt">Mensaje del usuario con la respuesta a corregir y la rúbrica.</param>
    /// <param name="tools">Definiciones de herramientas (tools) disponibles para el modelo (opcional).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta del LLM con contenido, métricas y posibles tool calls.</returns>
    Task<LlmCompletionResult> CompletarAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<LlmToolDefinition>? tools = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado de una llamada al LLM.</summary>
public class LlmCompletionResult
{
    /// <summary>Contenido textual de la respuesta del modelo.</summary>
    public string? Contenido { get; set; }

    /// <summary>Llamadas a herramientas solicitadas por el modelo.</summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>Proveedor utilizado (Groq, OpenRouter, Local).</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Modelo específico utilizado.</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>Tokens de entrada.</summary>
    public int TokensEntrada { get; set; }

    /// <summary>Tokens de salida.</summary>
    public int TokensSalida { get; set; }

    /// <summary>Latencia en milisegundos.</summary>
    public long LatenciaMs { get; set; }

    /// <summary>Indica si la respuesta incluye tool calls.</summary>
    public bool TieneToolCalls => ToolCalls is { Count: > 0 };
}

/// <summary>Definición de una herramienta que el LLM puede invocar.</summary>
public class LlmToolDefinition
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Schema JSON de los parámetros de la tool.</summary>
    public string ParametrosSchemaJson { get; set; } = "{}";
}

/// <summary>Invocación de herramienta solicitada por el LLM.</summary>
public class LlmToolCall
{
    public string Id { get; set; } = string.Empty;
    public string NombreTool { get; set; } = string.Empty;
    public string ArgumentosJson { get; set; } = "{}";
}
