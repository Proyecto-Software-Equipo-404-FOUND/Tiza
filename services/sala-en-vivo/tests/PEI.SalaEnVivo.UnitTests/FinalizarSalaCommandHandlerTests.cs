using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class FinalizarSalaCommandHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWorkContador _unitOfWork = new();

        private FinalizarSalaCommandHandler CrearHandler() =>
            new(_salaRepository, _unitOfWork);

        private Sala CrearSala(
            Guid salaId,
            Guid institucionId,
            string estado = "Activa",
            string? motivoCierre = null,
            DateTime? finalizadaUtc = null)
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                EvaluacionId = Guid.NewGuid(),
                CodigoPIN = 123456,
                Estado = estado,
                CapacidadMaxima = 50,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30),
                MotivoCierre = motivoCierre,
                FinalizadaUtc = finalizadaUtc
            };

            _salaRepository.Salas.Add(sala);
            return sala;
        }

        [Theory]
        [InlineData("Activa")]
        [InlineData("EnCurso")]
        public async Task Handle_SalaVigente_FinalizaConMotivoDocentePorDefecto(string estado)
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId, estado);
            var antes = DateTime.UtcNow;

            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(resultado.Valor);
            Assert.False(resultado.Valor!.YaEstabaFinalizada);
            Assert.Equal("Docente", resultado.Valor.Motivo);
            Assert.Equal("Finalizada", sala.Estado);
            Assert.Equal("Docente", sala.MotivoCierre);
            Assert.NotNull(sala.FinalizadaUtc);
            Assert.InRange(sala.FinalizadaUtc!.Value, antes, DateTime.UtcNow);
            Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        }

        [Fact]
        public async Task Handle_MotivoPersonalizado_UsaTiempoAgotado()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId);

            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(salaId, institucionId, "TiempoAgotado"),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(resultado.Valor);
            Assert.Equal("TiempoAgotado", resultado.Valor!.Motivo);
            Assert.False(resultado.Valor.YaEstabaFinalizada);
            Assert.Equal("Finalizada", sala.Estado);
            Assert.Equal("TiempoAgotado", sala.MotivoCierre);
            Assert.NotNull(sala.FinalizadaUtc);
            Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        }

        [Fact]
        public async Task Handle_SalaYaFinalizada_EsIdempotenteYNoGuardaCambios()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var finalizadaUtc = DateTime.UtcNow.AddMinutes(-5);
            var sala = CrearSala(
                salaId,
                institucionId,
                "Finalizada",
                "Docente",
                finalizadaUtc);

            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(salaId, institucionId, "TiempoAgotado"),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(resultado.Valor);
            Assert.True(resultado.Valor!.YaEstabaFinalizada);
            Assert.Equal(finalizadaUtc, resultado.Valor.FinalizadaUtc);
            Assert.Equal("Docente", resultado.Valor.Motivo);
            Assert.Equal(finalizadaUtc, sala.FinalizadaUtc);
            Assert.Equal("Docente", sala.MotivoCierre);
            Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
        }

        [Fact]
        public async Task Handle_SalaInexistente_DevuelveSalaNoEncontrada()
        {
            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SALA_NO_ENCONTRADA", resultado.Codigo);
            Assert.Null(resultado.Valor);
            Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
        }

        [Fact]
        public async Task Handle_InstitucionDistinta_DevuelveNoAutorizado()
        {
            var salaId = Guid.NewGuid();
            var institucionIdSala = Guid.NewGuid();
            var institucionIdComando = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionIdSala);

            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(salaId, institucionIdComando),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("NO_AUTORIZADO", resultado.Codigo);
            Assert.Null(resultado.Valor);
            Assert.Equal("Activa", sala.Estado);
            Assert.Null(sala.FinalizadaUtc);
            Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
        }
    }

    internal sealed class FakeUnitOfWorkContador : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
