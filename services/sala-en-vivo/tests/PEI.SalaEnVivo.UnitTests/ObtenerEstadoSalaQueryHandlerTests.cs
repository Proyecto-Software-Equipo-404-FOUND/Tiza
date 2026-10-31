using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Application.UseCases.Salas.Queries;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class ObtenerEstadoSalaQueryHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeParticipanteSalaRepository _participanteRepository = new();
        private readonly FakeRespuestaSalaRepository _respuestaRepository = new();
        private readonly FakeSalaCronometroService _cronometroService = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private ObtenerEstadoSalaQueryHandler CrearHandler()
        {
            return new ObtenerEstadoSalaQueryHandler(
                _salaRepository,
                _participanteRepository,
                _respuestaRepository,
                _cronometroService,
                _evaluacionesClient);
        }

        private Sala CrearSala(Guid salaId, Guid institucionId, string estado = "EnCurso")
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                CodigoPIN = 654321,
                Estado = estado,
                CapacidadMaxima = 40,
                CantidadParticipantes = 10,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(25)
            };
            _salaRepository.Salas.Add(sala);
            return sala;
        }

        [Fact]
        public async Task Handle_AlumnoEnSalaConRespuestasPrevias_RecuperaSecuenciaYPreguntasRespondidas()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var pregunta1 = Guid.NewGuid();
            var pregunta2 = Guid.NewGuid();

            CrearSala(salaId, institucionId, "EnCurso");
            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId, salaId, alumnoId, pregunta1, "Opción A", Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(-3)));
            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId, salaId, alumnoId, pregunta2, "Opción B", Guid.NewGuid(), 2, DateTime.UtcNow.AddMinutes(-1)));

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionId, "Alumno", salaId);

            var estado = await handler.Handle(query, CancellationToken.None);

            Assert.Equal(salaId, estado.SalaId);
            Assert.Equal("EnCurso", estado.Estado);
            Assert.Equal(2, estado.UltimaSecuencia);
            Assert.Equal(2, estado.PreguntasRespondidas.Count);
            Assert.Contains(pregunta1, estado.PreguntasRespondidas);
            Assert.Contains(pregunta2, estado.PreguntasRespondidas);
            Assert.NotNull(estado.FechaLimiteUtc);
        }

        [Fact]
        public async Task Handle_ReconexionSinSalaIdEnContexto_ResuelveUltimaSalaDelAlumno()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var pregunta1 = Guid.NewGuid();

            CrearSala(salaId, institucionId, "EnCurso");
            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            _respuestaRepository.Respuestas.Add(RespuestaSala.Crear(
                institucionId, salaId, alumnoId, pregunta1, "Respuesta 1", Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(-1)));

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionId, "Alumno", SalaId: null);

            var estado = await handler.Handle(query, CancellationToken.None);

            Assert.Equal(salaId, estado.SalaId);
            Assert.Equal(1, estado.UltimaSecuencia);
            Assert.Single(estado.PreguntasRespondidas);
            Assert.Equal(pregunta1, estado.PreguntasRespondidas[0]);
        }

        [Fact]
        public async Task Handle_DocenteEnSala_RetornaSecuenciaCeroYSinPreguntasRespondidas()
        {
            var salaId = Guid.NewGuid();
            var docenteId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSala(salaId, institucionId, "Activa");

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(docenteId, institucionId, "Docente", salaId);

            var estado = await handler.Handle(query, CancellationToken.None);

            Assert.Equal(salaId, estado.SalaId);
            Assert.Equal("Activa", estado.Estado);
            Assert.Equal(0, estado.UltimaSecuencia);
            Assert.Empty(estado.PreguntasRespondidas);
        }

        [Fact]
        public async Task Handle_SalaFinalizada_RetornaEstadoFinalizadaSinError()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSala(salaId, institucionId, "Finalizada");
            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionId, "Alumno", salaId);

            var estado = await handler.Handle(query, CancellationToken.None);

            Assert.Equal("Finalizada", estado.Estado);
            Assert.Null(estado.PreguntaActual);
        }

        [Fact]
        public async Task Handle_AlumnoNoRegistradoEnSala_LanzaInvalidOperationException()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();

            CrearSala(salaId, institucionId, "Activa");

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionId, "Alumno", salaId);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(query, CancellationToken.None));

            Assert.Contains("ALUMNO_NO_EN_SALA", ex.Message);
        }

        [Fact]
        public async Task Handle_SalaDeOtraInstitucion_LanzaUnauthorizedAccessException()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionSala = Guid.NewGuid();
            var institucionToken = Guid.NewGuid();

            CrearSala(salaId, institucionSala, "Activa");
            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionToken, "Alumno", salaId);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(query, CancellationToken.None));

            Assert.Contains("SALA_OTRA_INSTITUCION", ex.Message);
        }

        [Fact]
        public async Task Handle_SalaInexistente_LanzaKeyNotFoundException()
        {
            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(Guid.NewGuid(), Guid.NewGuid(), "Alumno", Guid.NewGuid());

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => handler.Handle(query, CancellationToken.None));

            Assert.Contains("SALA_NO_ENCONTRADA", ex.Message);
        }

        [Fact]
        public async Task Handle_SalaEnCursoConPreguntaVigente_RetornaPreguntaActual()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var pregunta1Id = Guid.NewGuid();
            var pregunta2Id = Guid.NewGuid();

            var sala = CrearSala(salaId, institucionId, "EnCurso");
            sala.EvaluacionId = evaluacionId;
            sala.EstablecerPreguntaActual(pregunta2Id, 2);

            _participanteRepository.Participantes.Add(new ParticipanteSala
            {
                Id = Guid.NewGuid(),
                SalaId = salaId,
                AlumnoId = alumnoId
            });

            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                new[]
                {
                    new PreguntaEvaluacionDto(pregunta1Id, "Pregunta 1", "Abierta", 5m, null),
                    new PreguntaEvaluacionDto(pregunta2Id, "Pregunta 2", "Abierta", 5m, null)
                });

            var handler = CrearHandler();
            var query = new ObtenerEstadoSalaQuery(alumnoId, institucionId, "Alumno", salaId);

            var estado = await handler.Handle(query, CancellationToken.None);

            Assert.NotNull(estado.PreguntaActual);
            Assert.Equal(pregunta2Id, estado.PreguntaActual!.PreguntaId);
            Assert.Equal(2, estado.PreguntaActual.Indice);
            Assert.Equal(2, estado.PreguntaActual.TotalPreguntas);
            Assert.Equal("Pregunta 2", estado.PreguntaActual.Enunciado);
        }
    }

    internal class FakeParticipanteSalaRepository : IParticipanteSalaRepository
    {
        public List<ParticipanteSala> Participantes { get; } = new();

        public Task AddAsync(ParticipanteSala participante, CancellationToken cancellationToken)
        {
            Participantes.Add(participante);
            return Task.CompletedTask;
        }

        public Task<bool> ExisteAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken) =>
            Task.FromResult(Participantes.Any(p => p.SalaId == salaId && p.AlumnoId == alumnoId));

        public Task<Guid?> ObtenerUltimaSalaIdDeAlumnoAsync(Guid alumnoId, CancellationToken cancellationToken = default)
        {
            var ultima = Participantes
                .LastOrDefault(p => p.AlumnoId == alumnoId);

            return Task.FromResult(ultima?.SalaId);
        }

        public Task<int> ContarAlumnosAsync(Guid salaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Participantes.Count(p => p.SalaId == salaId));
    }
}
