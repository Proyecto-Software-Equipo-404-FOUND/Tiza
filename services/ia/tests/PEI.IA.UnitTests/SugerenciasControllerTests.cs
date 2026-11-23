using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PEI.IA.Api.Controllers;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;
using Xunit;

namespace PEI.IA.UnitTests;

public class SugerenciasControllerTests
{
    private readonly FakeOrchestrator _fakeOrchestrator = new();
    private readonly SugerenciasController _controller;

    public SugerenciasControllerTests()
    {
        _controller = new SugerenciasController(
            _fakeOrchestrator,
            NullLogger<SugerenciasController>.Instance
        );
    }

    [Fact]
    public async Task SugerirPreguntas_MaterialIdVacio_Retorna400BadRequest()
    {
        var request = new SugerirPreguntasRequest
        {
            MaterialId = Guid.Empty,
            Cantidad = 5
        };

        var result = await _controller.SugerirPreguntas(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains("materialId", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task SugerirPreguntas_CantidadInvalida_Retorna400BadRequest(int cantidad)
    {
        var request = new SugerirPreguntasRequest
        {
            MaterialId = Guid.NewGuid(),
            Cantidad = cantidad
        };

        var result = await _controller.SugerirPreguntas(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("La cantidad tiene que estar entre 1 y 10.", problem.Title);
    }

    [Fact]
    public async Task SugerirPreguntas_TipoInvalido_Retorna400BadRequest()
    {
        var request = new SugerirPreguntasRequest
        {
            MaterialId = Guid.NewGuid(),
            Cantidad = 3,
            Tipos = new List<string> { "TipoInexistente" }
        };

        var result = await _controller.SugerirPreguntas(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("permitidos", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SugerirPreguntas_MaterialSinTexto_Retorna400ConTypeMaterialSinTexto()
    {
        var materialId = Guid.NewGuid();
        _fakeOrchestrator.LanzarExcepcion = new InvalidOperationException("MATERIAL_SIN_TEXTO: No hay texto");

        var request = new SugerirPreguntasRequest
        {
            MaterialId = materialId,
            Cantidad = 3
        };

        var result = await _controller.SugerirPreguntas(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("MATERIAL_SIN_TEXTO", problem.Type);
    }

    [Fact]
    public async Task SugerirPreguntas_PeticionValida_Retorna200OkConPreguntas()
    {
        var materialId = Guid.NewGuid();
        _fakeOrchestrator.RespuestaGenerar = new SugerirPreguntasResponse
        {
            MaterialId = materialId,
            Preguntas = new List<PreguntaSugeridaDto>
            {
                new() { Enunciado = "¿Pregunta 1?", Tipo = "OpcionMultiple", PuntajeMaximo = 10m }
            },
            RevisadaPorAgenteRevisor = true,
            Proveedor = "OpenRouter",
            Modelo = "nemotron"
        };

        var request = new SugerirPreguntasRequest
        {
            MaterialId = materialId,
            Cantidad = 1
        };

        var result = await _controller.SugerirPreguntas(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var respuesta = Assert.IsType<SugerirPreguntasResponse>(okResult.Value);
        Assert.Equal(materialId, respuesta.MaterialId);
        Assert.True(respuesta.RevisadaPorAgenteRevisor);
    }

    private sealed class FakeOrchestrator : IAgentesOrchestrator
    {
        public Exception? LanzarExcepcion { get; set; }
        public SugerirPreguntasResponse RespuestaGenerar { get; set; } = new();

        public Task<SugerirPreguntasResponse> GenerarPreguntasAsync(
            SugerirPreguntasRequest request,
            CancellationToken cancellationToken = default)
        {
            if (LanzarExcepcion != null)
            {
                throw LanzarExcepcion;
            }

            return Task.FromResult(RespuestaGenerar);
        }

        public Task<RevisionCorreccionResultado> RevisarCorreccionAsync(
            RevisarCorreccionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RevisionCorreccionResultado { EsConsistente = true });
        }
    }
}
