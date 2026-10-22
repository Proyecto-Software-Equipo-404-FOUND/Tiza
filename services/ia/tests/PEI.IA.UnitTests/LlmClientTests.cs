using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Application.Interfaces;
using PEI.IA.Infrastructure.LlmClients;

namespace PEI.IA.UnitTests;

public class LlmClientTests
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
    public async Task CompletarAsync_RespuestaExitosa_RetornaResultadoCorrecto()
    {
        // Arrange
        var fakeResponseJson = """
        {
            "id": "chatcmpl-123",
            "model": "llama-3.3-70b-versatile",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "{\"notaSugerida\": 8.5, \"justificacion\": \"Bien respondido\"}"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 120,
                "completion_tokens": 45,
                "total_tokens": 165
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.EndsWith("chat/completions", req.RequestUri?.ToString().TrimEnd('/'));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fakeResponseJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
        var settings = new LlmSettings
        {
            Provider = "Groq",
            ApiKey = "gsk-fake-key",
            BaseUrl = "https://api.groq.com/openai/v1",
            Model = "llama-3.3-70b-versatile"
        };
        var logger = NullLogger<OpenAiCompatibleClient>.Instance;
        var client = new OpenAiCompatibleClient(httpClient, settings, logger);

        // Act
        var result = await client.CompletarAsync("Sos un corrector", "Esta es la respuesta");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Groq", result.Proveedor);
        Assert.Equal("llama-3.3-70b-versatile", result.Modelo);
        Assert.Equal(120, result.TokensEntrada);
        Assert.Equal(45, result.TokensSalida);
        Assert.Contains("8.5", result.Contenido);
        Assert.True(result.LatenciaMs >= 0);
    }

    [Fact]
    public async Task CompletarAsync_ConToolCalling_RetornaToolCalls()
    {
        // Arrange
        var fakeToolCallResponseJson = """
        {
            "id": "chatcmpl-456",
            "model": "llama-3.3-70b-versatile",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc123",
                                "type": "function",
                                "function": {
                                    "name": "RegistrarCalificacionSugerida",
                                    "arguments": "{\"notaSugerida\":9.0,\"justificacion\":\"Excelente respuesta\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ],
            "usage": {
                "prompt_tokens": 150,
                "completion_tokens": 30,
                "total_tokens": 180
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(fakeToolCallResponseJson, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
        var settings = new LlmSettings { Provider = "Groq", ApiKey = "fake" };
        var client = new OpenAiCompatibleClient(httpClient, settings, NullLogger<OpenAiCompatibleClient>.Instance);

        var tools = new List<LlmToolDefinition>
        {
            new()
            {
                Nombre = "RegistrarCalificacionSugerida",
                Descripcion = "Registra calificación",
                ParametrosSchemaJson = "{\"type\":\"object\",\"properties\":{\"notaSugerida\":{\"type\":\"number\"}}}"
            }
        };

        // Act
        var result = await client.CompletarAsync("Sys", "User", tools);

        // Assert
        Assert.NotNull(result.ToolCalls);
        Assert.Single(result.ToolCalls);
        Assert.Equal("RegistrarCalificacionSugerida", result.ToolCalls[0].NombreTool);
        Assert.Equal("call_abc123", result.ToolCalls[0].Id);
        Assert.Contains("9.0", result.ToolCalls[0].ArgumentosJson);
    }

    [Fact]
    public async Task CompletarAsync_ErrorHttp_LanzaHttpRequestException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid API key\"}", Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
        var settings = new LlmSettings { Provider = "Groq", ApiKey = "bad-key" };
        var client = new OpenAiCompatibleClient(httpClient, settings, NullLogger<OpenAiCompatibleClient>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CompletarAsync("Sys", "User"));
    }
}
