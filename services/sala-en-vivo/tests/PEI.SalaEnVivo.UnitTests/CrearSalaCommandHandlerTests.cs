using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class CrearSalaCommandHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private CrearSalaCommandHandler CrearHandler() =>
            new(_unitOfWork, _salaRepository, _evaluacionesClient);

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(101)]
        public async Task HandleAsync_CapacidadFueraDeRango_DebeLanzarArgumentOutOfRangeException(int capacidad)
        {
            var handler = CrearHandler();
            var command = new CrearSalaCommand
            {
                InstitucionId = Guid.NewGuid(),
                EvaluacionId = Guid.NewGuid(),
                CapacidadMaxima = capacidad
            };

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                handler.HandleAsync(command, CancellationToken.None));
        }

        [Fact]
        public async Task HandleAsync_EvaluacionInexistente_DebeLanzarKeyNotFoundException()
        {
            var handler = CrearHandler();
            var command = new CrearSalaCommand
            {
                InstitucionId = Guid.NewGuid(),
                EvaluacionId = Guid.NewGuid(),
                CapacidadMaxima = 30
            };

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.HandleAsync(command, CancellationToken.None));

            Assert.Contains("evaluación no existe", ex.Message);
        }

        [Fact]
        public async Task HandleAsync_EvaluacionNoPublicada_DebeLanzarKeyNotFoundException()
        {
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Borrador",
                Array.Empty<PreguntaEvaluacionDto>());

            var handler = CrearHandler();
            var command = new CrearSalaCommand
            {
                InstitucionId = Guid.NewGuid(),
                EvaluacionId = evaluacionId,
                CapacidadMaxima = 30
            };

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.HandleAsync(command, CancellationToken.None));

            Assert.Contains("no está publicada", ex.Message);
        }

        [Fact]
        public async Task HandleAsync_EvaluacionPublicada_DebeCrearSalaConPinValido()
        {
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                Array.Empty<PreguntaEvaluacionDto>());

            var handler = CrearHandler();
            var command = new CrearSalaCommand
            {
                InstitucionId = institucionId,
                EvaluacionId = evaluacionId,
                CapacidadMaxima = 45
            };

            var sala = await handler.HandleAsync(command, CancellationToken.None);

            Assert.NotNull(sala);
            Assert.Equal(institucionId, sala.InstitucionId);
            Assert.Equal(evaluacionId, sala.EvaluacionId);
            Assert.Equal(45, sala.CapacidadMaxima);
            Assert.Equal("Activa", sala.Estado);
            Assert.InRange(sala.CodigoPIN, 100000, 999999);
            Assert.True(sala.FechaVencimiento > DateTime.UtcNow);
        }
    }
}
