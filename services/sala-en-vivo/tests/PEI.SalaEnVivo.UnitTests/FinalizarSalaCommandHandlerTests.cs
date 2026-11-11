using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly FakeParticipanteSalaRepository _participanteRepository = new();
        private readonly FakeRespuestaSalaRepository _respuestaRepository = new();
        private readonly FakeAuditoriaFocoRepository _auditoriaFocoRepository = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private FinalizarSalaCommandHandler CrearHandler() =>
            new(
                _salaRepository,
                _participanteRepository,
                _respuestaRepository,
                _auditoriaFocoRepository,
                _evaluacionesClient,
                _unitOfWork,
                NullLogger<FinalizarSalaCommandHandler>.Instance);

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

        private void RegistrarParticipante(Guid salaId, Guid alumnoId)
        {
            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });
        }

        [Theory]
        [InlineData("Activa")]
        [InlineData("EnCurso")]
        public async Task Handle_SalaVigente_FinalizaYEnviaLoteConMotivoDocente(string estado)
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var alumnoConRespuesta = Guid.NewGuid();
            var alumnoSinRespuesta = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId, estado);
            var antes = DateTime.UtcNow;

            RegistrarParticipante(salaId, alumnoConRespuesta);
            RegistrarParticipante(salaId, alumnoSinRespuesta);
            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId,
                salaId,
                alumnoConRespuesta,
                preguntaId,
                "opcion-a",
                Guid.NewGuid(),
                1));
            _auditoriaFocoRepository.Eventos.Add(AuditoriaFoco.Crear(
                institucionId,
                salaId,
                alumnoConRespuesta,
                3500,
                "visibilitychange",
                preguntaId));

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

            var envio = Assert.Single(_evaluacionesClient.LotesEnviados);
            Assert.Equal(salaId, envio.SalaId);

            var lote = envio.Lote;
            Assert.Equal(1, lote.Version);
            Assert.NotEqual(Guid.Empty, lote.LoteId);
            Assert.Equal(salaId, lote.SalaId);
            Assert.Equal(sala.EvaluacionId, lote.EvaluacionId);
            Assert.Equal(institucionId, lote.InstitucionId);
            Assert.Equal(sala.FinalizadaUtc!.Value, lote.FinalizadaUtc);
            Assert.Equal("Docente", lote.MotivoCierre);
            Assert.Equal(2, lote.Entregas.Count);

            var entregaConRespuesta = Assert.Single(
                lote.Entregas.Where(e => e.AlumnoId == alumnoConRespuesta));
            Assert.Single(entregaConRespuesta.Respuestas);
            Assert.Equal(preguntaId, entregaConRespuesta.Respuestas[0].PreguntaId);
            Assert.Equal("opcion-a", entregaConRespuesta.Respuestas[0].Respuesta);
            Assert.Single(entregaConRespuesta.AlertasFoco);
            Assert.Equal("Roja", entregaConRespuesta.AlertasFoco[0].Severidad);

            var entregaSinRespuesta = Assert.Single(
                lote.Entregas.Where(e => e.AlumnoId == alumnoSinRespuesta));
            Assert.Empty(entregaSinRespuesta.Respuestas);
            Assert.Empty(entregaSinRespuesta.AlertasFoco);
        }

        [Fact]
        public async Task Handle_MotivoPersonalizado_UsaTiempoAgotadoEnSalaYLote()
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

            var envio = Assert.Single(_evaluacionesClient.LotesEnviados);
            Assert.Equal("TiempoAgotado", envio.Lote.MotivoCierre);
            Assert.Empty(envio.Lote.Entregas);
        }

        [Fact]
        public async Task Handle_SalaYaFinalizada_EsIdempotenteYNoGuardaNiDespacha()
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
            Assert.Empty(_evaluacionesClient.LotesEnviados);
        }

        [Fact]
        public async Task Handle_SalaInexistente_DevuelveSalaNoEncontradaSinDespachar()
        {
            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SALA_NO_ENCONTRADA", resultado.Codigo);
            Assert.Null(resultado.Valor);
            Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
            Assert.Empty(_evaluacionesClient.LotesEnviados);
        }

        [Fact]
        public async Task Handle_InstitucionDistinta_DevuelveNoAutorizadoSinDespachar()
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
            Assert.Empty(_evaluacionesClient.LotesEnviados);
        }

        [Fact]
        public async Task Handle_FalloDeDespacho_MarcaEntregaPendienteYConservaFinalizacion()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId);
            _evaluacionesClient.FallarEnvio = true;

            var resultado = await CrearHandler().Handle(
                new FinalizarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(resultado.Valor);
            Assert.False(resultado.Valor!.YaEstabaFinalizada);
            Assert.Equal("Finalizada", sala.Estado);
            Assert.True(sala.EntregaPendiente);
            Assert.Equal(2, _unitOfWork.SaveChangesCallCount);
            Assert.Single(_evaluacionesClient.LotesEnviados);
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
