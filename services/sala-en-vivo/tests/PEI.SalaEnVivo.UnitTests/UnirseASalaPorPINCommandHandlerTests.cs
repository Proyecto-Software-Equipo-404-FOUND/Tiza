using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class UnirseASalaPorPINCommandHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeParticipanteSalaRepository _participantes = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();
        private readonly FakeCursosClient _cursosClient = new();

        private readonly Guid _institucionId = Guid.NewGuid();
        private readonly Guid _cohorteId = Guid.NewGuid();
        private readonly Guid _alumnoId = Guid.NewGuid();
        private const int Pin = 123456;

        private UnirseASalaPorPINCommandHandler CrearHandler() =>
            new(_unitOfWork, _salaRepository, _participantes, _evaluacionesClient, _cursosClient);

        private Sala CrearSalaConEvaluacion(Guid? cohorteId)
        {
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                Array.Empty<PreguntaEvaluacionDto>())
            {
                CohorteId = cohorteId
            };

            var sala = new Sala
            {
                Id = Guid.NewGuid(),
                InstitucionId = _institucionId,
                EvaluacionId = evaluacionId,
                CodigoPIN = Pin,
                Estado = "Activa",
                CapacidadMaxima = 10,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30)
            };
            _salaRepository.Salas.Add(sala);
            return sala;
        }

        private UnirseASalaPorPINCommand Comando(string? token = "token-alumno") =>
            new(Pin, _alumnoId, _institucionId, token);

        [Fact]
        public async Task Handle_AlumnoMatriculado_IngresaALaSala()
        {
            var sala = CrearSalaConEvaluacion(_cohorteId);
            _cursosClient.Matriculados.Add((_cohorteId, _alumnoId));

            var salaId = await CrearHandler().Handle(Comando(), CancellationToken.None);

            Assert.Equal(sala.Id, salaId);
            Assert.Single(_participantes.Participantes);
            Assert.Equal("token-alumno", _cursosClient.UltimoToken);
        }

        [Fact]
        public async Task Handle_AlumnoNoMatriculado_LanzaAlumnoNoMatriculadoYNoIngresa()
        {
            CrearSalaConEvaluacion(_cohorteId);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CrearHandler().Handle(Comando(), CancellationToken.None));

            Assert.StartsWith("ALUMNO_NO_MATRICULADO:", error.Message);
            Assert.Empty(_participantes.Participantes);
        }

        [Fact]
        public async Task Handle_CursosNoResponde_LanzaMatriculaNoVerificada()
        {
            CrearSalaConEvaluacion(_cohorteId);
            _cursosClient.Fallar = true;

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CrearHandler().Handle(Comando(), CancellationToken.None));

            Assert.StartsWith("MATRICULA_NO_VERIFICADA:", error.Message);
            Assert.Empty(_participantes.Participantes);
        }

        [Fact]
        public async Task Handle_SinToken_LanzaMatriculaNoVerificada()
        {
            CrearSalaConEvaluacion(_cohorteId);
            _cursosClient.Matriculados.Add((_cohorteId, _alumnoId));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CrearHandler().Handle(Comando(token: null), CancellationToken.None));

            Assert.StartsWith("MATRICULA_NO_VERIFICADA:", error.Message);
        }

        [Fact]
        public async Task Handle_EvaluacionSinCohorte_NoValidaMatricula()
        {
            CrearSalaConEvaluacion(cohorteId: null);

            await CrearHandler().Handle(Comando(), CancellationToken.None);

            Assert.Single(_participantes.Participantes);
            Assert.Null(_cursosClient.UltimoToken);
        }
    }

    internal class FakeCursosClient : ICursosClient
    {
        public HashSet<(Guid CohorteId, Guid AlumnoId)> Matriculados { get; } = new();
        public bool Fallar { get; set; }
        public string? UltimoToken { get; private set; }

        public Task<bool> AlumnoTieneMatriculaActivaAsync(
            Guid cohorteId,
            Guid alumnoId,
            string tokenAlumno,
            CancellationToken cancellationToken = default)
        {
            UltimoToken = tokenAlumno;

            if (Fallar)
            {
                throw new HttpRequestException("Cursos no responde");
            }

            return Task.FromResult(Matriculados.Contains((cohorteId, alumnoId)));
        }
    }
}
