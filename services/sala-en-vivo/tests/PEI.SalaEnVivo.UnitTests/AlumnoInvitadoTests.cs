using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using PEI.SalaEnVivo.Api.Controllers;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    // S3-SA-01 (#231): alumno invitado sin cuenta, solo si el docente lo habilita en la sala.
    public class AlumnoInvitadoTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeParticipanteSalaRepository _participantes = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();
        private readonly FakeCursosClient _cursosClient = new();
        private readonly Guid _institucionId = Guid.NewGuid();
        private const int Pin = 482913;

        private Sala CrearSala(bool permitirInvitados)
        {
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId, "Publicada", Array.Empty<PreguntaEvaluacionDto>())
            {
                CohorteId = Guid.NewGuid()
            };

            var sala = new Sala
            {
                Id = Guid.NewGuid(),
                InstitucionId = _institucionId,
                EvaluacionId = evaluacionId,
                CodigoPIN = Pin,
                Estado = "Activa",
                CapacidadMaxima = 10,
                FechaVencimiento = DateTime.UtcNow.AddHours(1),
                PermitirInvitados = permitirInvitados
            };
            _salaRepository.Salas.Add(sala);
            return sala;
        }

        private UnirseASalaPorPINCommandHandler CrearHandler() =>
            new(_unitOfWork, _salaRepository, _participantes, _evaluacionesClient, _cursosClient);

        private UnirseASalaPorPINCommand ComandoInvitado(Guid salaDelToken, Guid invitadoId) =>
            new(Pin, invitadoId, _institucionId, "token-invitado", true, salaDelToken, "Lucía Fernández");

        [Fact]
        public async Task Invitado_SalaQueAdmiteInvitados_EntraSinValidarMatriculaYGuardaElNombre()
        {
            var sala = CrearSala(permitirInvitados: true);
            var invitadoId = Guid.NewGuid();

            var salaId = await CrearHandler().Handle(ComandoInvitado(sala.Id, invitadoId), CancellationToken.None);

            Assert.Equal(sala.Id, salaId);
            var participante = Assert.Single(_participantes.Participantes);
            Assert.Equal(invitadoId, participante.AlumnoId);
            Assert.Equal("Lucía Fernández", participante.NombreInvitado);
            Assert.Null(_cursosClient.UltimoToken); // no se consultó la matrícula
        }

        [Fact]
        public async Task Invitado_SalaSinInvitados_SeRechazaConSalaSinInvitados()
        {
            var sala = CrearSala(permitirInvitados: false);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CrearHandler().Handle(ComandoInvitado(sala.Id, Guid.NewGuid()), CancellationToken.None));

            Assert.StartsWith("SALA_SIN_INVITADOS", ex.Message);
            Assert.Empty(_participantes.Participantes);
        }

        [Fact]
        public async Task Invitado_TokenDeOtraSala_NoEntra()
        {
            CrearSala(permitirInvitados: true);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CrearHandler().Handle(ComandoInvitado(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));

            Assert.Empty(_participantes.Participantes);
        }

        [Fact]
        public async Task AlumnoConCuenta_SiSigueValidandoMatricula()
        {
            CrearSala(permitirInvitados: true);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CrearHandler().Handle(
                    new UnirseASalaPorPINCommand(Pin, Guid.NewGuid(), _institucionId, "token-alumno"),
                    CancellationToken.None));

            Assert.StartsWith("ALUMNO_NO_MATRICULADO", ex.Message);
        }

        [Fact]
        public async Task Interno_SinClave_401_YConClave_DevuelveLaSala()
        {
            var sala = CrearSala(permitirInvitados: true);
            var configuracion = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ServiciosInternos:ApiKey"] = "clave-test" })
                .Build();
            var controller = new SalasInternoController(_salaRepository, configuracion);

            var sinClave = await controller.ObtenerParaInvitado(Pin, null, CancellationToken.None);
            var conClave = await controller.ObtenerParaInvitado(Pin, "clave-test", CancellationToken.None);
            var otroPin = await controller.ObtenerParaInvitado(111111, "clave-test", CancellationToken.None);

            Assert.Equal(401, Assert.IsType<ObjectResult>(sinClave).StatusCode);
            var ok = Assert.IsType<OkObjectResult>(conClave);
            var tipo = ok.Value!.GetType();
            Assert.Equal(sala.Id, tipo.GetProperty("salaId")!.GetValue(ok.Value));
            Assert.Equal(true, tipo.GetProperty("permiteInvitados")!.GetValue(ok.Value));
            Assert.Equal(404, Assert.IsType<ObjectResult>(otroPin).StatusCode);
        }
    }
}
