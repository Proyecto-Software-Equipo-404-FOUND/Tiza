using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Infrastructure.Evaluaciones;

namespace PEI.IA.UnitTests;

public class EvaluacionesHttpClientTests
{
    private static readonly Guid EntregaIdTest  = Guid.NewGuid();
    private static readonly Guid PreguntaIdTest = Guid.NewGuid();
    private const decimal NotaTest = 8.5m;
    private const string JustificacionTest = "Respuesta fundamentada según la rúbrica.";
    private const string DesgloseTest = "[{\"criterio\":\"Conceptos\",\"puntajeMaximo\":10,\"puntajeOtorgado\":8.5}]";

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarSugerenciaAsync_CuandoServicioResponde200Ok_RetornaTrue()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true, ApiKeyInterna = "dev-interna-pei-2026" };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        // Act
        var resultado = await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest, PreguntaIdTest);

        // Assert
        Assert.True(resultado);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains($"interno/entregas/{EntregaIdTest}/calificacion-sugerida",
            capturedRequest.RequestUri?.ToString());
    }

    // ── Header X-Api-Key-Interna ────────────────────────────────────────────

    [Fact]
    public async Task RegistrarSugerenciaAsync_EnviaHeaderXApiKeyInterna()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true, ApiKeyInterna = "dev-interna-pei-2026" };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        // Act
        await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest, PreguntaIdTest);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Api-Key-Interna"),
            "El header X-Api-Key-Interna debe estar presente en el request.");
        Assert.Equal("dev-interna-pei-2026",
            capturedRequest.Headers.GetValues("X-Api-Key-Interna").First());
    }

    [Fact]
    public async Task RegistrarSugerenciaAsync_SinApiKeyConfigurada_NoAgregaHeader()
    {
        // Sin clave → no debe enviar header vacío (evita 401 con header presente pero vacío)
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true, ApiKeyInterna = "" };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest, PreguntaIdTest);

        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest.Headers.Contains("X-Api-Key-Interna"));
    }

    // ── PreguntaId en body JSON ─────────────────────────────────────────────

    [Fact]
    public async Task RegistrarSugerenciaAsync_EnviaPreguntaIdEnBodyJson()
    {
        // Arrange
        string? requestBody = null;
        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            if (req.Content != null)
                requestBody = await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true, ApiKeyInterna = "key" };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        // Act
        await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest, PreguntaIdTest);

        // Assert
        Assert.NotNull(requestBody);
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        Assert.Equal(PreguntaIdTest.ToString(), root.GetProperty("preguntaId").GetString());
        Assert.Equal(NotaTest, root.GetProperty("notaSugerida").GetDecimal());
        Assert.Equal(JustificacionTest, root.GetProperty("justificacion").GetString());
        Assert.Equal(DesgloseTest, root.GetProperty("desgloseJson").GetString());
    }

    [Fact]
    public async Task RegistrarSugerenciaAsync_CuandoPreguntaIdEsNull_EnviaNullEnBody()
    {
        // Arrange
        string? requestBody = null;
        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            if (req.Content != null)
                requestBody = await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true, ApiKeyInterna = "key" };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        // Act
        await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest, preguntaId: null);

        // Assert
        Assert.NotNull(requestBody);
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("preguntaId").ValueKind);
    }

    // ── Errores HTTP ────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarSugerenciaAsync_CuandoServicioResponde500InternalServerError_RetornaFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"Database connection failed\"}")
            }));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        var resultado = await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest);

        Assert.False(resultado);
    }

    [Fact]
    public async Task RegistrarSugerenciaAsync_CuandoOcurreHttpRequestException_RetornaFalseSinLanzarExcepcion()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            throw new HttpRequestException("No se pudo establecer conexión con el host de Evaluaciones."));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        var resultado = await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest);

        Assert.False(resultado);
    }

    [Fact]
    public async Task RegistrarSugerenciaAsync_CuandoOcurreTimeout_RetornaFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            throw new TaskCanceledException("The operation was canceled due to timeout."));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = true };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        var resultado = await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest);

        Assert.False(resultado);
    }

    // ── Modo stub ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarSugerenciaAsync_ModoStubDeshabilitado_RetornaTrueSinLlamadaHttp()
    {
        var fueLlamado = false;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            fueLlamado = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5103/") };
        var settings = new EvaluacionesSettings { HabilitarIntegracion = false };
        var client = new EvaluacionesHttpClient(http, settings, NullLogger<EvaluacionesHttpClient>.Instance);

        var resultado = await client.RegistrarSugerenciaAsync(
            EntregaIdTest, NotaTest, JustificacionTest, DesgloseTest);

        Assert.True(resultado);
        Assert.False(fueLlamado, "En modo stub no debe realizarse ninguna llamada HTTP real.");
    }

    // ── Helper HttpMessageHandler ───────────────────────────────────────────
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
