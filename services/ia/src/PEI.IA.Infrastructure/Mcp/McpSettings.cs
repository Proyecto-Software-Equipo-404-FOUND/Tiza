namespace PEI.IA.Infrastructure.Mcp;

/// <summary>
/// Configuración del servidor MCP y del modo de ejecución de tools.
/// </summary>
public class McpSettings
{
    /// <summary>URL del servidor MCP remoto.</summary>
    public string McpServerUrl { get; set; } = "http://localhost:5106/mcp";

    /// <summary>
    /// Si es true, usa el LocalToolExecutor (Plan B) en lugar de conectar al servidor MCP.
    /// Útil para desarrollo local o si el servidor MCP presenta problemas.
    /// </summary>
    public bool UsarPlanB { get; set; } = true;
}
