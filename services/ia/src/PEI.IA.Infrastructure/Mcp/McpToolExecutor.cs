using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.Mcp;

/// <summary>
/// Ejecutor de tools via servidor MCP remoto usando JSON-RPC 2.0 sobre transporte HTTP.
/// Si el servidor MCP remoto no está disponible o falla, delega automáticamente en LocalToolExecutor (resiliencia Plan B).
/// </summary>
public class McpToolExecutor : IToolExecutor
{
    private readonly HttpClient _httpClient;
    private readonly McpSettings _settings;
    private readonly ILogger<McpToolExecutor> _logger;
    private readonly LocalToolExecutor _fallback;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public McpToolExecutor(
        HttpClient httpClient,
        McpSettings settings,
        ILogger<McpToolExecutor> logger,
        LocalToolExecutor fallback)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<LlmToolDefinition>> ListarToolsAsync(CancellationToken cancellationToken = default)
    {
        if (_settings.UsarPlanB)
        {
            _logger.LogInformation("[McpToolExecutor] UsarPlanB activo, delegando listado a LocalToolExecutor");
            return await _fallback.ListarToolsAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation("[McpToolExecutor] Solicitando tools al servidor MCP: {Url}", _settings.McpServerUrl);

            var rpcRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            };

            var content = new StringContent(JsonSerializer.Serialize(rpcRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_settings.McpServerUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("result", out var resultEl) &&
                    resultEl.TryGetProperty("tools", out var toolsArray) &&
                    toolsArray.ValueKind == JsonValueKind.Array)
                {
                    var result = new List<LlmToolDefinition>();
                    foreach (var tool in toolsArray.EnumerateArray())
                    {
                        var name = tool.GetProperty("name").GetString() ?? string.Empty;
                        var desc = tool.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                        var schema = tool.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{}";

                        result.Add(new LlmToolDefinition
                        {
                            Nombre = name,
                            Descripcion = desc,
                            ParametrosSchemaJson = schema
                        });
                    }

                    if (result.Count > 0)
                    {
                        _logger.LogInformation("[McpToolExecutor] Se obtuvieron {Count} tools del servidor MCP remoto", result.Count);
                        return result;
                    }
                }
            }

            _logger.LogWarning("[McpToolExecutor] Respuesta no exitosa de MCP ({Status}). Usando fallback local.", response.StatusCode);
            return await _fallback.ListarToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[McpToolExecutor] Falló la comunicación con el servidor MCP remoto ({Url}). Activando fallback local.", _settings.McpServerUrl);
            return await _fallback.ListarToolsAsync(cancellationToken);
        }
    }

    public async Task<string> EjecutarAsync(string toolName, string argumentosJson, CancellationToken cancellationToken = default)
    {
        if (_settings.UsarPlanB)
        {
            return await _fallback.EjecutarAsync(toolName, argumentosJson, cancellationToken);
        }

        try
        {
            _logger.LogInformation("[McpToolExecutor] Invocando tool remota '{Tool}' via JSON-RPC en {Url}", toolName, _settings.McpServerUrl);

            JsonElement argsElement;
            try
            {
                argsElement = JsonDocument.Parse(argumentosJson).RootElement;
            }
            catch
            {
                argsElement = JsonDocument.Parse("{}").RootElement;
            }

            var rpcRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = argsElement
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(rpcRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_settings.McpServerUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("[McpToolExecutor] Tool '{Tool}' ejecutada exitosamente en MCP remoto", toolName);
                return responseJson;
            }

            _logger.LogWarning("[McpToolExecutor] Error HTTP {Status} en llamada remota. Usando fallback local.", response.StatusCode);
            return await _fallback.EjecutarAsync(toolName, argumentosJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[McpToolExecutor] Error al conectar con MCP remoto. Ejecutando tool '{Tool}' en fallback local.", toolName);
            return await _fallback.EjecutarAsync(toolName, argumentosJson, cancellationToken);
        }
    }
}
