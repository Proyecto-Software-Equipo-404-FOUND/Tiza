using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PEI.SalaEnVivo.Application.DTOs.Entregas;
using PEI.SalaEnVivo.Infrastructure.Clientes;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class EvaluacionesClientTests
    {
        private const string TestApiKey = "clave-interna-test-2026";

        private static IConfiguration CrearConfiguracion(string apiKey = TestApiKey)
        {
            var configData = new Dictionary<string, string?>
            {
                ["ServiciosInternos:ApiKey"] = apiKey
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public int Invocaciones { get; private set; }

            public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Invocaciones++;
                return Task.FromResult(_responder(request));
            }
        }

        private static LoteEntregasRequestDto CrearLote(Guid salaId, Guid loteId)
        {
            return new LoteEntregasRequestDto(
                Version: 1,
                LoteId: loteId,
                SalaId: salaId,
                EvaluacionId: Guid.NewGuid(),
                InstitucionId: Guid.NewGuid(),
                FinalizadaUtc: DateTime.UtcNow,
                MotivoCierre: "Docente",
                Entregas: new List<EntregaAlumnoDto>());
        }

        [Fact]
        public async Task EnviarLoteEntregasAsync_RespuestaExitosa_RetornaResultado()
        {
            var salaId = Guid.NewGuid();
            var loteId = Guid.NewGuid();

            var handler = new TestHttpMessageHandler(req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Equal($"/interno/salas/{salaId}/entregas", req.RequestUri?.AbsolutePath);
                Assert.True(req.Headers.Contains("X-Api-Key-Interna"));
                Assert.Equal(TestApiKey, req.Headers.GetValues("X-Api-Key-Interna").First());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ResultadoLoteEntregasDto(
                        loteId,
                        salaId,
                        EntregasRecibidas: 1,
                        EntregasNuevas: 1,
                        EntregasYaExistentes: 0,
                        Entregas: new List<EntregaResultadoDto>
                        {
                            new(Guid.NewGuid(), Guid.NewGuid(), "Corregida", 10)
                        }))
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var client = new EvaluacionesClient(httpClient, CrearConfiguracion());

            var resultado = await client.EnviarLoteEntregasAsync(
                salaId,
                CrearLote(salaId, loteId));

            Assert.NotNull(resultado);
            Assert.Equal(loteId, resultado!.LoteId);
            Assert.Equal(1, handler.Invocaciones);
        }

        [Fact]
        public async Task EnviarLoteEntregasAsync_Error400_NoReintentaYRetornaNull()
        {
            var salaId = Guid.NewGuid();
            var loteId = Guid.NewGuid();

            var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"PAYLOAD_INVALIDO\"}")
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var client = new EvaluacionesClient(httpClient, CrearConfiguracion());

            var resultado = await client.EnviarLoteEntregasAsync(
                salaId,
                CrearLote(salaId, loteId));

            Assert.Null(resultado);
            Assert.Equal(1, handler.Invocaciones);
        }

        [Fact]
        public async Task EnviarLoteEntregasAsync_Error500_ReintentaHasta3VecesYRetornaNull()
        {
            var salaId = Guid.NewGuid();
            var loteId = Guid.NewGuid();

            var handler = new TestHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var client = new EvaluacionesClient(httpClient, CrearConfiguracion());

            var resultado = await client.EnviarLoteEntregasAsync(
                salaId,
                CrearLote(salaId, loteId));

            Assert.Null(resultado);
            Assert.Equal(3, handler.Invocaciones);
        }
    }
}
