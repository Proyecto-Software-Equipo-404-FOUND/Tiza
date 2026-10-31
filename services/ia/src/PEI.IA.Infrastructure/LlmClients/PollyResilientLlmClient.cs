using Microsoft.Extensions.Logging;
using PEI.IA.Application.Interfaces;
using Polly;
using Polly.CircuitBreaker;

namespace PEI.IA.Infrastructure.LlmClients;

/// <summary>
/// Cliente LLM resiliente que orquesta múltiples proveedores (ej. Groq -> OpenRouter -> Local)
/// utilizando Polly para conmutación automática (fallback) y Circuit Breaker ante errores 429 (TooManyRequests),
/// 5xx o Timeouts.
/// </summary>
public class PollyResilientLlmClient : ILlmClient
{
    private readonly IReadOnlyList<ProviderEntry> _providers;
    private readonly LlmResilienceOptions _resilienceOptions;
    private readonly ILogger<PollyResilientLlmClient> _logger;

    public record ProviderEntry(
        LlmProviderConfig Config,
        ILlmClient Client,
        ResiliencePipeline<LlmCompletionResult> Pipeline);

    /// <summary>
    /// Constructor principal usado por DI / Host Web.
    /// </summary>
    public PollyResilientLlmClient(
        IHttpClientFactory httpClientFactory,
        LlmSettings settings,
        LlmResilienceOptions resilienceOptions,
        ILoggerFactory loggerFactory)
    {
        _resilienceOptions = resilienceOptions;
        _logger = loggerFactory.CreateLogger<PollyResilientLlmClient>();

        var configs = settings.ObtenerProveedoresOrdenados();
        var entries = new List<ProviderEntry>();

        foreach (var config in configs)
        {
            var httpClient = httpClientFactory.CreateClient($"LlmProvider_{config.Provider}");
            var clientLogger = loggerFactory.CreateLogger<OpenAiCompatibleClient>();
            var client = new OpenAiCompatibleClient(httpClient, config, clientLogger);

            var pipeline = ConstruirPipeline(config, _resilienceOptions);
            entries.Add(new ProviderEntry(config, client, pipeline));
        }

        _providers = entries;
    }

    /// <summary>
    /// Constructor para tests unitarios que permite inyectar clientes mock o fakes directamente.
    /// </summary>
    public PollyResilientLlmClient(
        IReadOnlyList<(LlmProviderConfig Config, ILlmClient Client)> providers,
        LlmResilienceOptions resilienceOptions,
        ILogger<PollyResilientLlmClient> logger)
    {
        _resilienceOptions = resilienceOptions;
        _logger = logger;

        var entries = new List<ProviderEntry>();
        foreach (var (config, client) in providers)
        {
            var pipeline = ConstruirPipeline(config, _resilienceOptions);
            entries.Add(new ProviderEntry(config, client, pipeline));
        }

        _providers = entries;
    }

    /// <inheritdoc />
    public async Task<LlmCompletionResult> CompletarAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<LlmToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        if (_providers.Count == 0)
        {
            throw new InvalidOperationException("No hay ningún proveedor LLM configurado en PollyResilientLlmClient.");
        }

        var fallos = new List<string>();

        foreach (var entry in _providers)
        {
            var providerName = entry.Config.Provider;

            try
            {
                _logger.LogInformation(
                    "[PollyResilientLlmClient] Intentando con proveedor {Provider} (Prioridad: {Prioridad}, Modelo: {Model})",
                    providerName, entry.Config.Prioridad, entry.Config.Model);

                var result = await entry.Pipeline.ExecuteAsync(
                    async token => await entry.Client.CompletarAsync(systemPrompt, userPrompt, tools, token),
                    cancellationToken);

                _logger.LogInformation(
                    "[PollyResilientLlmClient] Éxito con proveedor {Provider} (Modelo: {Model}). Tokens: {In}→{Out}, Latencia: {Latencia}ms",
                    result.Proveedor, result.Modelo, result.TokensEntrada, result.TokensSalida, result.LatenciaMs);

                return result;
            }
            catch (BrokenCircuitException bce)
            {
                _logger.LogWarning(
                    "[PollyResilientLlmClient] Circuit Breaker ABIERTO para {Provider}: {Msg}. Conmutando al siguiente proveedor.",
                    providerName, bce.Message);

                fallos.Add($"{providerName}: Circuito abierto");
            }
            catch (Exception ex) when (LlmResilienceOptions.EsFalloConmutable(ex))
            {
                _logger.LogWarning(ex,
                    "[PollyResilientLlmClient] Fallo conmutable (429/timeout/red) en proveedor {Provider}: {Msg}. Conmutando al siguiente.",
                    providerName, ex.Message);

                fallos.Add($"{providerName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PollyResilientLlmClient] Fallo crítico no conmutable en proveedor {Provider}.",
                    providerName);
                throw;
            }
        }

        var detalle = string.Join("; ", fallos);
        throw new InvalidOperationException(
            $"Todos los proveedores LLM fallaron ({_providers.Count} intentados). Detalle: {detalle}");
    }

    private static ResiliencePipeline<LlmCompletionResult> ConstruirPipeline(
        LlmProviderConfig config,
        LlmResilienceOptions options)
    {
        return new ResiliencePipelineBuilder<LlmCompletionResult>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<LlmCompletionResult>
            {
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(options.DuracionAperturaCircuitoSegundos * 2),
                MinimumThroughput = Math.Max(2, options.FallosConsecutivosParaAbrirCircuito),
                BreakDuration = TimeSpan.FromSeconds(options.DuracionAperturaCircuitoSegundos),
                ShouldHandle = new PredicateBuilder<LlmCompletionResult>()
                    .Handle<Exception>(LlmResilienceOptions.EsFalloConmutable)
            })
            .Build();
    }
}
