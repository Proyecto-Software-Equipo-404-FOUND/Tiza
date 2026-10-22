using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;
using PEI.IA.Application.Services;
using PEI.IA.Infrastructure.Mcp;

namespace PEI.IA.UnitTests;

public class McpToolExecutorTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task ListarToolsAsync_ConServidorMcpActivo_ParseaToolsJsonRpcCorrectamente()
    {
        var jsonRpcResponse = """
        {
            "jsonrpc": "2.0",
            "id": 1,
            "result": {
                "tools": [
                    {
                        "name": "RegistrarCalificacionSugerida",
                        "description": "Registra calificación pedagógica",
                        "inputSchema": { "type": "object" }
                    }
                ]
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonRpcResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var settings = new McpSettings { UsarPlanB = false, McpServerUrl = "http://localhost:5106/mcp" };
        var fallback = new LocalToolExecutor(NullLogger<LocalToolExecutor>.Instance);
        var executor = new McpToolExecutor(httpClient, settings, NullLogger<McpToolExecutor>.Instance, fallback);

        var tools = await executor.ListarToolsAsync();

        Assert.NotNull(tools);
        Assert.Single(tools);
        Assert.Equal("RegistrarCalificacionSugerida", tools[0].Nombre);
    }

    [Fact]
    public async Task ListarToolsAsync_ServidorMcpCaido_ActivaFallbackLocalSinLanzarExcepcion()
    {
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var httpClient = new HttpClient(handler);
        var settings = new McpSettings { UsarPlanB = false, McpServerUrl = "http://localhost:5106/mcp" };
        var fallback = new LocalToolExecutor(NullLogger<LocalToolExecutor>.Instance);
        var executor = new McpToolExecutor(httpClient, settings, NullLogger<McpToolExecutor>.Instance, fallback);

        var tools = await executor.ListarToolsAsync();

        Assert.NotNull(tools);
        Assert.NotEmpty(tools);
        Assert.Contains(tools, t => t.Nombre == "RegistrarCalificacionSugerida");
    }

    [Fact]
    public async Task CorreccionService_OrquestaLlmYToolCallingExitosamente()
    {
        // Mock LlmClient que devuelve un ToolCall
        var mockLlm = new FakeLlmClient(new LlmCompletionResult
        {
            Proveedor = "Groq",
            Modelo = "llama-3.3-70b-versatile",
            TokensEntrada = 120,
            TokensSalida = 40,
            LatenciaMs = 280,
            ToolCalls = new List<LlmToolCall>
            {
                new()
                {
                    Id = "call_1",
                    NombreTool = "RegistrarCalificacionSugerida",
                    ArgumentosJson = """
                    {
                        "notaSugerida": 9.5,
                        "justificacion": "Respuesta sobresaliente con ejemplos precisos.",
                        "desglosePorCriterio": [
                            {
                                "criterio": "Concepto principal",
                                "puntajeMaximo": 10.0,
                                "puntajeOtorgado": 9.5,
                                "comentario": "Excelente explicación."
                            }
                        ]
                    }
                    """
                }
            }
        });

        var promptBuilder = new PEI.IA.Application.Prompts.PromptBuilder();
        var toolExecutor = new LocalToolExecutor(NullLogger<LocalToolExecutor>.Instance);
        var service = new CorreccionService(mockLlm, promptBuilder, toolExecutor, NullLogger<CorreccionService>.Instance);

        var request = new CorregirRespuestaRequest
        {
            PreguntaEnunciado = "¿Qué es el encapsulamiento?",
            PuntajeMaximo = 10,
            RubricaJson = "{\"criterios\":[]}",
            RespuestaTexto = "Es el ocultamiento del estado interno de un objeto."
        };

        var response = await service.CorregirAsync(request);

        Assert.NotNull(response);
        Assert.Equal(9.5m, response.NotaSugerida);
        Assert.Equal("Groq", response.Proveedor);
        Assert.Contains("sobresaliente", response.Justificacion);
        Assert.NotNull(response.DesglosePorCriterio);
        Assert.Single(response.DesglosePorCriterio);
        Assert.Equal(9.5m, response.DesglosePorCriterio[0].PuntajeOtorgado);
    }

    private class FakeLlmClient : ILlmClient
    {
        private readonly LlmCompletionResult _result;

        public FakeLlmClient(LlmCompletionResult result)
        {
            _result = result;
        }

        public Task<LlmCompletionResult> CompletarAsync(string systemPrompt, string userPrompt, IReadOnlyList<LlmToolDefinition>? tools = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}
