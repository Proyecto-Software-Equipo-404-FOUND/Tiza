using ModelContextProtocol.Server;
using System.ComponentModel;

namespace PEI.IA.McpServer.Tools;

/// <summary>
/// Tool MCP de prueba que devuelve el texto recibido.
/// Sirve para validar que el transporte HTTP y el SDK C# funcionan correctamente.
/// Se eliminará o reemplazará por tools reales en IA1-02.
/// </summary>
[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Devuelve el texto recibido como eco. Tool de prueba para validar la conectividad MCP.")]
    public static string Echo(
        [Description("Texto a devolver como eco")] string texto)
    {
        return $"[Echo MCP] {texto}";
    }

    [McpServerTool, Description("Devuelve información del servidor MCP de IA de Tiza.")]
    public static object ServerInfo()
    {
        return new
        {
            servicio = "PEI.IA.McpServer",
            version = "1.0.0",
            protocolo = "MCP",
            transporte = "HTTP Stateless",
            timestamp = DateTime.UtcNow
        };
    }
}
