using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Infrastructure.LlmClients;

/// <summary>
/// Cliente LLM que se comunica con cualquier proveedor compatible con la API de OpenAI
/// (Groq, OpenRouter, servidor local con vLLM/Ollama, etc.).
/// Soporta tool calling para integración con MCP.
/// </summary>
public class OpenAiCompatibleClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmSettings _settings;
    private readonly ILogger<OpenAiCompatibleClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public OpenAiCompatibleClient(
        HttpClient httpClient,
        LlmSettings settings,
        ILogger<OpenAiCompatibleClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async Task<LlmCompletionResult> CompletarAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<LlmToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _settings.Model,
            ["messages"] = messages,
            ["temperature"] = _settings.Temperature,
            ["max_tokens"] = _settings.MaxTokens,
        };

        // Agregar tools si se proporcionaron
        if (tools is { Count: > 0 })
        {
            var openAiTools = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Nombre,
                    description = t.Descripcion,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.ParametrosSchemaJson)
                }
            }).ToList();

            requestBody["tools"] = openAiTools;
            requestBody["tool_choice"] = "auto";
        }

        var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

        _logger.LogDebug("[LLM {Provider}/{Model}] Enviando request ({Bytes} bytes)",
            _settings.Provider, _settings.Model, jsonContent.Length);

        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("chat/completions", httpContent, cancellationToken);

        sw.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[LLM {Provider}] Error {Status}: {Body}",
                _settings.Provider, response.StatusCode, responseJson[..Math.Min(500, responseJson.Length)]);

            throw new HttpRequestException(
                $"Error del proveedor LLM {_settings.Provider}: {response.StatusCode} - {responseJson[..Math.Min(200, responseJson.Length)]}");
        }

        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);

        var result = new LlmCompletionResult
        {
            Proveedor = _settings.Provider,
            Modelo = chatResponse?.Model ?? _settings.Model,
            TokensEntrada = chatResponse?.Usage?.PromptTokens ?? 0,
            TokensSalida = chatResponse?.Usage?.CompletionTokens ?? 0,
            LatenciaMs = sw.ElapsedMilliseconds
        };

        var choice = chatResponse?.Choices?.FirstOrDefault();
        if (choice != null)
        {
            result.Contenido = choice.Message?.Content;

            if (choice.Message?.ToolCalls is { Count: > 0 })
            {
                result.ToolCalls = choice.Message.ToolCalls.Select(tc => new LlmToolCall
                {
                    Id = tc.Id ?? string.Empty,
                    NombreTool = tc.Function?.Name ?? string.Empty,
                    ArgumentosJson = tc.Function?.Arguments ?? "{}"
                }).ToList();
            }
        }

        _logger.LogInformation(
            "[LLM {Provider}/{Model}] Completado en {Latencia}ms — Tokens: {In}→{Out}, ToolCalls: {TC}",
            result.Proveedor, result.Modelo, result.LatenciaMs,
            result.TokensEntrada, result.TokensSalida,
            result.ToolCalls?.Count ?? 0);

        return result;
    }
}

// ── Modelos de deserialización de la respuesta OpenAI ──

internal class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; set; }
}

internal class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class ChatMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ChatToolCall>? ToolCalls { get; set; }
}

internal class ChatToolCall
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public ChatFunctionCall? Function { get; set; }
}

internal class ChatFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

internal class ChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
