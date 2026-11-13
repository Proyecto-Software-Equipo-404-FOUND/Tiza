using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.SalaEnVivo.Api.Controllers;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class SalasControllerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private SalasController CrearController(ClaimsPrincipal? principal = null)
        {
            var handler = new CrearSalaCommandHandler(_unitOfWork, _salaRepository, _evaluacionesClient);
            var controller = new SalasController(handler);

            var httpContext = new DefaultHttpContext();
            if (principal != null)
            {
                httpContext.User = principal;
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private static ClaimsPrincipal CrearDocentePrincipal(Guid institucionId)
        {
            var claims = new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("institucion_id", institucionId.ToString()),
                new Claim("role", "Docente")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task CrearSala_SinClaimInstitucion_DebeRetornar401Unauthorized()
        {
            // Arrange: usuario autenticado pero sin institucion_id
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("role", "Docente")
            }, "TestAuth"));

            var controller = CrearController(principal);
            var request = new CrearSalaRequestDto(Guid.NewGuid(), 40);

            // Act
            var result = await controller.CrearSala(request, CancellationToken.None);

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);
            Assert.Contains("institucion_id", problem.Title);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(101)]
        public async Task CrearSala_CapacidadInvalida_DebeRetornar400BadRequest(int capacidadInvalida)
        {
            // Arrange
            var institucionId = Guid.NewGuid();
            var controller = CrearController(CrearDocentePrincipal(institucionId));
            var request = new CrearSalaRequestDto(Guid.NewGuid(), capacidadInvalida);

            // Act
            var result = await controller.CrearSala(request, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task CrearSala_EvaluacionInexistente_DebeRetornar404NotFound()
        {
            // Arrange: evaluación no existe en EvaluacionesClient
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var controller = CrearController(CrearDocentePrincipal(institucionId));
            var request = new CrearSalaRequestDto(evaluacionId, 30);

            // Act
            var result = await controller.CrearSala(request, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(notFound.Value);
            Assert.Contains("evaluación no existe", problem.Title);
        }

        [Fact]
        public async Task CrearSala_EvaluacionNoPublicada_DebeRetornar404NotFound()
        {
            // Arrange: evaluación en estado Borrador
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Borrador",
                Array.Empty<PreguntaEvaluacionDto>());

            var controller = CrearController(CrearDocentePrincipal(institucionId));
            var request = new CrearSalaRequestDto(evaluacionId, 30);

            // Act
            var result = await controller.CrearSala(request, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task CrearSala_DocenteConEvaluacionPublicada_DebeRetornar201CreatedConLocationYPin()
        {
            // Arrange: evaluación en estado Publicada
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                Array.Empty<PreguntaEvaluacionDto>());

            var controller = CrearController(CrearDocentePrincipal(institucionId));
            var request = new CrearSalaRequestDto(evaluacionId, 25);

            // Act
            var result = await controller.CrearSala(request, CancellationToken.None);

            // Assert
            var created = Assert.IsType<CreatedResult>(result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            Assert.StartsWith("/api/v1/salas/", created.Location);

            // Verificar que se guardó en el repositorio
            Assert.Single(_salaRepository.Salas);
            var salaGuardada = _salaRepository.Salas[0];
            Assert.Equal(institucionId, salaGuardada.InstitucionId);
            Assert.Equal(evaluacionId, salaGuardada.EvaluacionId);
            Assert.Equal(25, salaGuardada.CapacidadMaxima);
            Assert.InRange(salaGuardada.CodigoPIN, 100000, 999999);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(241)]
        public async Task CrearSala_DuracionInvalida_DebeRetornar400BadRequest(int duracion)
        {
            var controller = CrearController(CrearDocentePrincipal(Guid.NewGuid()));
            var request = new CrearSalaRequestDto(Guid.NewGuid(), 30, duracion);

            var result = await controller.CrearSala(request, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData(null, 120)]
        [InlineData(45, 45)]
        public async Task CrearSala_Duracion_DefineElVencimiento(int? duracion, int minutosEsperados)
        {
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                Array.Empty<PreguntaEvaluacionDto>());
            var controller = CrearController(CrearDocentePrincipal(Guid.NewGuid()));
            var antes = DateTime.UtcNow;

            var result = await controller.CrearSala(
                new CrearSalaRequestDto(evaluacionId, 30, duracion), CancellationToken.None);

            Assert.IsType<CreatedResult>(result);
            var vencimiento = _salaRepository.Salas[0].FechaVencimiento;
            Assert.InRange(
                vencimiento,
                antes.AddMinutes(minutosEsperados).AddSeconds(-5),
                DateTime.UtcNow.AddMinutes(minutosEsperados).AddSeconds(5));
        }
    }
}
