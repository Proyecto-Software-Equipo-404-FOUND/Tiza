namespace PEI.IA.Infrastructure.LlmClients;

/// <summary>
/// Configuración del cliente LLM compatible con la API de OpenAI.
/// Soporta Groq, OpenRouter, servidor local, etc.
/// </summary>
public class LlmSettings
{
    /// <summary>Nombre del proveedor (Groq, OpenRouter, Local, Gemini).</summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>API Key del proveedor.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL base del endpoint compatible con OpenAI (sin /chat/completions).</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>Modelo a utilizar.</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    /// <summary>Temperatura del modelo (0.0 = determinista, 1.0 = creativo).</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>Máximo de tokens de salida.</summary>
    public int MaxTokens { get; set; } = 2048;
}
