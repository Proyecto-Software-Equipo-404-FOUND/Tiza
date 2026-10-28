using Microsoft.Extensions.Logging.Abstractions;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class EnviarRespuestaCommandHandlerTests
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeRespuestaSalaRepository _respuestaRepository = new();
        private readonly FakeSalaCronometroService _cronometroService = new();

        private EnviarRespuestaCommandHandler CrearHandler()
        {
            return new EnviarRespuestaCommandHandler(
                _unitOfWork,
                _salaRepository,
                _respuestaRepository,
                _cronometroService,
                NullLogger<EnviarRespuestaCommandHandler>.Instance);
        }

        private Sala CrearSalaActiva(Guid salaId, Guid institucionId)
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                CodigoPIN = 123456,
                Estado = "Activa",
                CapacidadMaxima = 50,
                CantidadParticipantes = 5,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30)
            };
            _salaRepository.Salas.Add(sala);
            return sala;
        }

        [Fact]
        public async Task Handle_PrimerEnvioValido_RetornaAceptada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();

            CrearSalaActiva(salaId, institucionId);
            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                preguntaId,
                "Opción A seleccionada",
                idempotencyKey,
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Aceptada", resultado.Estado);
            Assert.Equal(preguntaId, resultado.PreguntaId);
            Assert.Equal(idempotencyKey, resultado.IdempotencyKey);
            Assert.Equal(1, resultado.Secuencia);
            Assert.Null(resultado.Error);
            Assert.Single(_respuestaRepository.Respuestas);
            Assert.Equal("Opción A seleccionada", _respuestaRepository.Respuestas[0].Respuesta);
        }

        [Fact]
        public async Task Handle_ReintentoMismaClaveYPayload_RetornaDuplicadaConTimestampOriginal()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();
            var fechaOriginal = DateTime.UtcNow.AddSeconds(-10);

            CrearSalaActiva(salaId, institucionId);
            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId,
                salaId,
                alumnoId,
                preguntaId,
                "Respuesta previa",
                idempotencyKey,
                1,
                fechaOriginal));

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                preguntaId,
                "  Respuesta previa  ", // Debe coincidir con trim
                idempotencyKey,
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Duplicada", resultado.Estado);
            Assert.Equal(fechaOriginal, resultado.RecibidaUtc);
            Assert.Null(resultado.Error);
            Assert.Single(_respuestaRepository.Respuestas); // No debe duplicar en lista
        }

        [Fact]
        public async Task Handle_IdempotencyKeyReutilizadaConDistintaRespuesta_RetornaRechazada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();

            CrearSalaActiva(salaId, institucionId);
            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId,
                salaId,
                alumnoId,
                preguntaId,
                "Respuesta original",
                idempotencyKey,
                1));

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                preguntaId,
                "Respuesta cambiada maliciosamente",
                idempotencyKey,
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("IDEMPOTENCY_KEY_REUTILIZADA", resultado.Error.Codigo);
            Assert.False(resultado.Error.Reintentable);
        }

        [Fact]
        public async Task Handle_TiempoAgotadoPorCronometro_RetornaRechazada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            CrearSalaActiva(salaId, institucionId);
            _cronometroService.ForzarVencida = true;

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                Guid.NewGuid(),
                "Respuesta tardía",
                Guid.NewGuid(),
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("TIEMPO_AGOTADO", resultado.Error.Codigo);
            Assert.False(resultado.Error.Reintentable);
        }

        [Fact]
        public async Task Handle_PreguntaYaRespondidaConOtraClave_RetornaRechazada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();

            CrearSalaActiva(salaId, institucionId);
            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId,
                salaId,
                alumnoId,
                preguntaId,
                "Primera respuesta registrada",
                Guid.NewGuid(),
                1));

            var handler = CrearHandler();

            // Intento de responder la misma pregunta con otra clave de idempotencia
            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                preguntaId,
                "Segunda respuesta no permitida",
                Guid.NewGuid(),
                2);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("RESPUESTA_YA_REGISTRADA", resultado.Error.Codigo);
            Assert.False(resultado.Error.Reintentable);
        }

        [Fact]
        public async Task Handle_SecuenciaFueraDeOrden_RetornaRechazadaReintentable()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSalaActiva(salaId, institucionId);
            var handler = CrearHandler();

            // Última secuencia es 0, y el alumno envía secuencia 3 directamente
            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                Guid.NewGuid(),
                "Respuesta salteada",
                Guid.NewGuid(),
                3);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("SECUENCIA_FUERA_DE_ORDEN", resultado.Error.Codigo);
            Assert.True(resultado.Error.Reintentable); // Debe ser reintentable tras sincronizar
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Handle_RespuestaVaciaOEspacios_RetornaRechazada(string respuestaInvalida)
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            CrearSalaActiva(salaId, institucionId);

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                Guid.NewGuid(),
                respuestaInvalida,
                Guid.NewGuid(),
                1);

            var task = handler.Handle(command, CancellationToken.None);
            Assert.Equal("Rechazada", task.Result.Estado);
            Assert.Equal("RESPUESTA_INVALIDA", task.Result.Error?.Codigo);
        }

        [Fact]
        public async Task Handle_SalaFinalizada_RetornaRechazada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var sala = CrearSalaActiva(salaId, institucionId);
            sala.Estado = "Finalizada";

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                Guid.NewGuid(),
                "Respuesta sobre sala cerrada",
                Guid.NewGuid(),
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("SALA_FINALIZADA", resultado.Error.Codigo);
        }

        [Fact]
        public async Task Handle_SalaOtraInstitucion_RetornaRechazada()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionIdToken = Guid.NewGuid();
            var institucionIdSala = Guid.NewGuid();

            CrearSalaActiva(salaId, institucionIdSala);

            var handler = CrearHandler();

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionIdToken, // No coincide con institucionIdSala
                Guid.NewGuid(),
                "Respuesta no autorizada",
                Guid.NewGuid(),
                1);

            var resultado = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("Rechazada", resultado.Estado);
            Assert.NotNull(resultado.Error);
            Assert.Equal("SALA_OTRA_INSTITUCION", resultado.Error.Codigo);
        }
    }

    #region Fakes de Prueba

    internal class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    internal class FakeSalaRepository : ISalaRepository
    {
        public List<Sala> Salas { get; } = new();

        public Task AddAsync(Sala sala, CancellationToken cancellationToken)
        {
            Salas.Add(sala);
            return Task.CompletedTask;
        }

        public Task<bool> ExisteActivaPorPINAsync(int pin, CancellationToken cancellationToken) =>
            Task.FromResult(Salas.Any(s => s.CodigoPIN == pin && s.Estado == "Activa"));

        public Task<Sala?> ObtenerActivaPorPINAsync(int pin, CancellationToken cancellationToken) =>
            Task.FromResult(Salas.FirstOrDefault(s => s.CodigoPIN == pin && s.Estado == "Activa"));

        public Task<Sala?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Salas.FirstOrDefault(s => s.Id == id));

        public Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ITransaccion>(new FakeTransaccion());
    }

    internal class FakeTransaccion : ITransaccion
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    internal class FakeRespuestaSalaRepository : IRespuestaSalaRepository
    {
        public List<RespuestaSala> Respuestas { get; } = new();

        public Task AddAsync(RespuestaSala respuesta, CancellationToken cancellationToken = default)
        {
            Respuestas.Add(respuesta);
            return Task.CompletedTask;
        }

        public Task<bool> ExisteRespuestaAPreguntaAsync(Guid salaId, Guid alumnoId, Guid preguntaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Respuestas.Any(r => r.SalaId == salaId && r.AlumnoId == alumnoId && r.PreguntaId == preguntaId));

        public Task<RespuestaSala?> ObtenerPorClaveIdempotenciaAsync(Guid salaId, Guid alumnoId, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Respuestas.FirstOrDefault(r => r.SalaId == salaId && r.AlumnoId == alumnoId && r.IdempotencyKey == idempotencyKey));

        public Task<List<Guid>> ObtenerPreguntasRespondidasAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Respuestas.Where(r => r.SalaId == salaId && r.AlumnoId == alumnoId).Select(r => r.PreguntaId).ToList());

        public Task<int> ObtenerUltimaSecuenciaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken = default)
        {
            var secuencias = Respuestas
                .Where(r => r.SalaId == salaId && r.AlumnoId == alumnoId)
                .Select(r => r.Secuencia)
                .ToList();

            return Task.FromResult(secuencias.Count > 0 ? secuencias.Max() : 0);
        }
    }

    internal class FakeSalaCronometroService : ISalaCronometroService
    {
        public bool ForzarVencida { get; set; }

        public Task EstablecerVencimientoAsync(Guid salaId, DateTime fechaVencimientoUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> EstaVencidaAsync(Guid salaId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(ForzarVencida);

        public Task<DateTime?> ObtenerVencimientoAsync(Guid salaId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTime?>(DateTime.UtcNow.AddMinutes(30));
    }

    #endregion
}
