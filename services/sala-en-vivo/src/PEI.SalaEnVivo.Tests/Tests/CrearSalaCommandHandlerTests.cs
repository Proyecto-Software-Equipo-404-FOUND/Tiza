using Moq;
using Xunit;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Tests.UseCases.Salas
{
    public class CrearSalaCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISalaRepository> _salaRepositoryMock;
        private readonly CrearSalaCommandHandler _handler;

        public CrearSalaCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _salaRepositoryMock = new Mock<ISalaRepository>();

            _handler = new CrearSalaCommandHandler(
                _unitOfWorkMock.Object,
                _salaRepositoryMock.Object
            );

            // Simulate saving successfully.
            _unitOfWorkMock
                .Setup(x => x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _salaRepositoryMock
                .Setup(x => x.AddAsync(
                    It.IsAny<Sala>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task HandleAsync_ShouldGenerateValidPIN()
        {
            // Arrange
            var command = new CrearSalaCommand
            {
                InstitucionId = Guid.NewGuid(),
                CapacidadMaxima = 10
            };

            _salaRepositoryMock
                .Setup(x => x.ExisteActivaPorPINAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.HandleAsync(
                command,
                CancellationToken.None);

            // Assert
            Assert.InRange(result.CodigoPIN, 100000, 999999);
        }
    }
}
