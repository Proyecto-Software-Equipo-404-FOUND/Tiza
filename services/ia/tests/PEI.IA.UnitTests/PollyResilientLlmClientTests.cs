using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Application.Interfaces;
using PEI.IA.Infrastructure.LlmClients;

namespace PEI.IA.UnitTests;

public class PollyResilientLlmClientTests
{
    private readonly LlmProviderConfig _configGroq = new()
    {
        Provider = "Groq",
        Model = "llama-3.3-70b",
        Prioridad = 1
    };

    private readonly LlmProviderConfig _configOpenRouter = new()
    {
        Provider = "OpenRouter",
        Model = "gemini-2.0-flash",
        Prioridad = 2
    };

    private readonly LlmResilienceOptions _options = new()
    {
        FallosConsecutivosParaAbrirCircuito = 2,
        DuracionAperturaCircuitoSegundos = 30
    };

    [Fact]
    public async Task CompletarAsync_CuandoProveedorPrincipalRespondeOk_UsaProveedorPrincipal()
    {
        // Arrange
        var mockGroq = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "Groq",
            Modelo = "llama-3.3-70b",
            Contenido = "Respuesta Groq"
        });

        var mockOpenRouter = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "OpenRouter",
            Modelo = "gemini-2.0-flash",
            Contenido = "Respuesta OpenRouter"
        });

        var providers = new List<(LlmProviderConfig, ILlmClient)>
        {
            (_configGroq, mockGroq),
            (_configOpenRouter, mockOpenRouter)
        };

        var client = new PollyResilientLlmClient(providers, _options, NullLogger<PollyResilientLlmClient>.Instance);

        // Act
        var result = await client.CompletarAsync("system", "user");

        // Assert
        Assert.Equal("Groq", result.Proveedor);
        Assert.Equal("Respuesta Groq", result.Contenido);
        Assert.Equal(1, mockGroq.LlamadasRealizadas);
        Assert.Equal(0, mockOpenRouter.LlamadasRealizadas);
    }

    [Fact]
    public async Task CompletarAsync_CuandoProveedorPrincipalDevuelve429TooManyRequests_ConmutaASecundarioExitosamente()
    {
        // Arrange
        var mockGroq = new FakeLlmClient(ex =>
            throw new HttpRequestException("Rate limit 429", null, HttpStatusCode.TooManyRequests));

        var mockOpenRouter = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "OpenRouter",
            Modelo = "gemini-2.0-flash",
            Contenido = "Respuesta desde OpenRouter",
            TokensEntrada = 100,
            TokensSalida = 50
        });

        var providers = new List<(LlmProviderConfig, ILlmClient)>
        {
            (_configGroq, mockGroq),
            (_configOpenRouter, mockOpenRouter)
        };

        var client = new PollyResilientLlmClient(providers, _options, NullLogger<PollyResilientLlmClient>.Instance);

        // Act
        var result = await client.CompletarAsync("system", "user");

        // Assert
        Assert.Equal("OpenRouter", result.Proveedor);
        Assert.Equal("Respuesta desde OpenRouter", result.Contenido);
        Assert.Equal(1, mockGroq.LlamadasRealizadas);
        Assert.Equal(1, mockOpenRouter.LlamadasRealizadas);
    }

    [Fact]
    public async Task CompletarAsync_CuandoProveedorPrincipalTimeout_ConmutaASecundarioExitosamente()
    {
        // Arrange
        var mockGroq = new FakeLlmClient(ex =>
            throw new TaskCanceledException("Timeout al conectar con Groq."));

        var mockOpenRouter = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "OpenRouter",
            Modelo = "gemini-2.0-flash",
            Contenido = "Respuesta tras timeout de Groq"
        });

        var providers = new List<(LlmProviderConfig, ILlmClient)>
        {
            (_configGroq, mockGroq),
            (_configOpenRouter, mockOpenRouter)
        };

        var client = new PollyResilientLlmClient(providers, _options, NullLogger<PollyResilientLlmClient>.Instance);

        // Act
        var result = await client.CompletarAsync("system", "user");

        // Assert
        Assert.Equal("OpenRouter", result.Proveedor);
        Assert.Equal(1, mockGroq.LlamadasRealizadas);
        Assert.Equal(1, mockOpenRouter.LlamadasRealizadas);
    }

    [Fact]
    public async Task CompletarAsync_CuandoTodosLosProveedoresFallan_LanzaInvalidOperationException()
    {
        // Arrange
        var mockGroq = new FakeLlmClient(ex =>
            throw new HttpRequestException("Groq 429", null, HttpStatusCode.TooManyRequests));

        var mockOpenRouter = new FakeLlmClient(ex =>
            throw new HttpRequestException("OpenRouter 503", null, HttpStatusCode.ServiceUnavailable));

        var providers = new List<(LlmProviderConfig, ILlmClient)>
        {
            (_configGroq, mockGroq),
            (_configOpenRouter, mockOpenRouter)
        };

        var client = new PollyResilientLlmClient(providers, _options, NullLogger<PollyResilientLlmClient>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompletarAsync("system", "user"));

        Assert.Contains("Todos los proveedores LLM fallaron", ex.Message);
        Assert.Contains("Groq", ex.Message);
        Assert.Contains("OpenRouter", ex.Message);
    }

    [Fact]
    public async Task CompletarAsync_CircuitBreakerAbierto_EvitaLlamadasAdicionalesAlProveedorFallido()
    {
        // Arrange
        var mockGroq = new FakeLlmClient(ex =>
            throw new HttpRequestException("Groq 429", null, HttpStatusCode.TooManyRequests));

        var mockOpenRouter = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "OpenRouter",
            Modelo = "gemini-2.0-flash",
            Contenido = "Fallback OK"
        });

        var providers = new List<(LlmProviderConfig, ILlmClient)>
        {
            (_configGroq, mockGroq),
            (_configOpenRouter, mockOpenRouter)
        };

        var client = new PollyResilientLlmClient(providers, _options, NullLogger<PollyResilientLlmClient>.Instance);

        // Act - 1ra llamada: falla Groq (falla 1) -> conmuta a OpenRouter
        await client.CompletarAsync("system", "user");

        // Act - 2da llamada: falla Groq (falla 2) -> abre circuito en Groq -> conmuta a OpenRouter
        await client.CompletarAsync("system", "user");

        var llamadasGroqAntesDe3ra = mockGroq.LlamadasRealizadas;

        // Act - 3ra llamada: circuito en Groq está ABIERTO -> salta directo a OpenRouter sin invocar a Groq
        var result3 = await client.CompletarAsync("system", "user");

        // Assert
        Assert.Equal("OpenRouter", result3.Proveedor);
        Assert.Equal(2, llamadasGroqAntesDe3ra);
        Assert.Equal(2, mockGroq.LlamadasRealizadas); // No aumentó a 3 porque el circuito estaba abierto
        Assert.Equal(3, mockOpenRouter.LlamadasRealizadas);
    }

    // ── Helper Mock LlmClient ───────────────────────────────────────────────
    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly LlmCompletionResult? _resultado;
        private readonly Action<object>? _onLlamada;

        public int LlamadasRealizadas { get; private set; }

        public FakeLlmClient(LlmCompletionResult resultado)
        {
            _resultado = resultado;
        }

        public FakeLlmClient(Action<object> onLlamada)
        {
            _onLlamada = onLlamada;
        }

        public Task<LlmCompletionResult> CompletarAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<LlmToolDefinition>? tools = null,
            CancellationToken cancellationToken = default)
        {
            LlamadasRealizadas++;

            if (_onLlamada != null)
            {
                _onLlamada(this);
            }

            return Task.FromResult(_resultado!);
        }
    }
}
