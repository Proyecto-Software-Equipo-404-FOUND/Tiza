namespace PEI.IA.Infrastructure.LlmClients;

/// <summary>
/// Configuración de un proveedor de LLM individual compatible con la API de OpenAI.
/// </summary>
public class LlmProviderConfig
{
    /// <summary>Nombre descriptivo del proveedor (ej. Groq, OpenRouter, Gemini, Local).</summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>API Key para autenticación Bearer con el proveedor.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL base del endpoint compatible con OpenAI (sin /chat/completions).</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>Nombre del modelo a utilizar en el proveedor.</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    /// <summary>Temperatura para la generación (0.0 = determinista, 1.0 = creativo).</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>Máximo de tokens de salida.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Timeout específico para este proveedor en milisegundos (por defecto 15s).</summary>
    public int TimeoutMs { get; set; } = 15000;

    /// <summary>Prioridad de ejecución (1 = principal, 2 = primer fallback, 3 = segundo fallback, etc.).</summary>
    public int Prioridad { get; set; } = 1;
}

/// <summary>
/// Configuración del cliente LLM compatible con la API de OpenAI.
/// Soporta configuración de proveedor único (retrocompatible) o múltiples proveedores con fallback.
/// </summary>
public class LlmSettings
{
    /// <summary>Nombre del proveedor principal (Groq, OpenRouter, Local, Gemini).</summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>API Key del proveedor principal.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL base del endpoint compatible con OpenAI (sin /chat/completions).</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>Modelo a utilizar en el proveedor principal.</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    /// <summary>Temperatura del modelo (0.0 = determinista, 1.0 = creativo).</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>Máximo de tokens de salida.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Timeout en milisegundos para llamadas al LLM.</summary>
    public int TimeoutMs { get; set; } = 15000;

    /// <summary>
    /// Lista de proveedores configurados ordenados por prioridad.
    /// Si está vacía o no configurada, se genera automáticamente un proveedor único a partir de las propiedades raíz.
    /// </summary>
    public List<LlmProviderConfig> Providers { get; set; } = new();

    /// <summary>
    /// Retorna la lista efectiva de proveedores ordenados por prioridad ascendente (1 primero).
    /// </summary>
    public IReadOnlyList<LlmProviderConfig> ObtenerProveedoresOrdenados()
    {
        if (Providers is { Count: > 0 })
        {
            return Providers.OrderBy(p => p.Prioridad).ToList();
        }

        return new List<LlmProviderConfig>
        {
            new()
            {
                Provider = Provider,
                ApiKey = ApiKey,
                BaseUrl = BaseUrl,
                Model = Model,
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                TimeoutMs = TimeoutMs,
                Prioridad = 1
            }
        };
    }
}
