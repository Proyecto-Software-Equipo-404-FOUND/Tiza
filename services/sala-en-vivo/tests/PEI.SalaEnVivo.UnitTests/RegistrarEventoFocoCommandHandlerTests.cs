using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class RegistrarEventoFocoCommandHandlerTests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeParticipanteSalaRepository _participanteRepository = new();
        private readonly FakeAuditoriaFocoRepository _auditoriaFocoRepository = new();

        private RegistrarEventoFocoCommandHandler CrearHandler() =>
            new(_unitOfWork, _salaRepository, _participanteRepository, _auditoriaFocoRepository);

        private Sala CrearSalaConAlumno(Guid salaId, Guid institucionId, Guid alumnoId, string estado = "EnCurso")
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                CodigoPIN = 654321,
                Estado = estado,
                CapacidadMaxima = 40,
                CantidadParticipantes = 1,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30)
            };
            _salaRepository.Salas.Add(sala);

            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            return sala;
        }

        [Fact]
        public async Task Handle_AusenciaMenorATresSegundos_RegistraAlertaAmarillo()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();

            var sala = CrearSalaConAlumno(salaId, institucionId, alumnoId);
            sala.EstablecerPreguntaActual(preguntaId, 1);

            var handler = CrearHandler();
            var resultado = await handler.Handle(
                new RegistrarEventoFocoCommand(salaId, alumnoId, institucionId, 1800, "visibilitychange"),
                CancellationToken.None);

            Assert.Equal("Amarillo", resultado.Severidad);
            Assert.Equal(1800, resultado.DuracionMs);
            Assert.Equal(preguntaId, resultado.PreguntaId);
            Assert.Single(_auditoriaFocoRepository.Eventos);
        }

        [Theory]
        [InlineData(3000)]
        [InlineData(5200)]
        public async Task Handle_AusenciaMayorOIgualATresSegundos_RegistraAlertaRoja(int duracionMs)
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSalaConAlumno(salaId, institucionId, alumnoId);

            var handler = CrearHandler();
            var resultado = await handler.Handle(
                new RegistrarEventoFocoCommand(salaId, alumnoId, institucionId, duracionMs, "blur"),
                CancellationToken.None);

            Assert.Equal("Roja", resultado.Severidad);
            Assert.Equal(duracionMs, resultado.DuracionMs);
            Assert.Equal("blur", resultado.TipoEvento);
            Assert.Single(_auditoriaFocoRepository.Eventos);
        }

        [Fact]
        public async Task Handle_MultiplesEventosDelMismoAlumno_PersisteAppendOnlySinSobrescribir()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSalaConAlumno(salaId, institucionId, alumnoId);
            var handler = CrearHandler();

            await handler.Handle(
                new RegistrarEventoFocoCommand(salaId, alumnoId, institucionId, 1200, "blur"),
                CancellationToken.None);
            await handler.Handle(
                new RegistrarEventoFocoCommand(salaId, alumnoId, institucionId, 4100, "visibilitychange"),
                CancellationToken.None);

            var eventos = await _auditoriaFocoRepository.ObtenerPorAlumnoEnSalaAsync(salaId, alumnoId);
            Assert.Equal(2, eventos.Count);
            Assert.Equal("Amarillo", eventos[0].Severidad);
            Assert.Equal("Roja", eventos[1].Severidad);
        }

        [Fact]
        public async Task Handle_AlumnoNoEnSala_LanzaInvalidOperationException()
        {
            var salaId = Guid.NewGuid();
            var alumnoRegistrado = Guid.NewGuid();
            var otroAlumno = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSalaConAlumno(salaId, institucionId, alumnoRegistrado);
            var handler = CrearHandler();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(
                    new RegistrarEventoFocoCommand(salaId, otroAlumno, institucionId, 2000),
                    CancellationToken.None));

            Assert.Contains("ALUMNO_NO_EN_SALA", ex.Message);
        }

        [Fact]
        public async Task Handle_SalaFinalizada_LanzaInvalidOperationException()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSalaConAlumno(salaId, institucionId, alumnoId, "Finalizada");
            var handler = CrearHandler();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(
                    new RegistrarEventoFocoCommand(salaId, alumnoId, institucionId, 3500),
                    CancellationToken.None));

            Assert.Contains("SALA_FINALIZADA", ex.Message);
        }
    }

    internal class FakeAuditoriaFocoRepository : IAuditoriaFocoRepository
    {
        public List<AuditoriaFoco> Eventos { get; } = new();

        public Task AddAsync(AuditoriaFoco evento, CancellationToken cancellationToken = default)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorSalaAsync(Guid salaId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditoriaFoco>>(
                Eventos.Where(x => x.SalaId == salaId).OrderBy(x => x.RegistradoUtc).ToList());

        public Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditoriaFoco>>(
                Eventos.Where(x => x.SalaId == salaId && x.AlumnoId == alumnoId).OrderBy(x => x.RegistradoUtc).ToList());
    }
}
