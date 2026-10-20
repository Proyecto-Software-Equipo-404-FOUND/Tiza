using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.Mcp;

/// <summary>
/// Ejecutor de tools via servidor MCP remoto.
/// Se completará con la integración real en IA2-03.
/// Por ahora es un stub que delega al LocalToolExecutor.
/// </summary>
public class McpToolExecutor : IToolExecutor
{
    private readonly ILogger<McpToolExecutor> _logger;
    private readonly LocalToolExecutor _fallback;

    public McpToolExecutor(ILogger<McpToolExecutor> logger, ILogger<LocalToolExecutor> fallbackLogger)
    {
        _logger = logger;
        _fallback = new LocalToolExecutor(fallbackLogger);
    }

    public async Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MCP] Ejecutando tool '{Tool}' - delegando a fallback local (IA2-03 pendiente)", toolName);

        // TODO (IA2-03): Conectar al servidor MCP real via ModelContextProtocol client SDK
        // Por ahora delega al executor local como fallback
        return await _fallback.EjecutarAsync(toolName, argumentosJson, cancellationToken);
    }

    public async Task<IReadOnlyList<LlmToolDefinition>> ListarToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MCP] Listando tools - delegando a fallback local (IA2-03 pendiente)");

        // TODO (IA2-03): Descubrir tools del servidor MCP real
        return await _fallback.ListarToolsAsync(cancellationToken);
    }
}
