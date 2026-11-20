using System;
using System.Collections.Generic;
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
    // S3-SA-02 (#239): el reloj de la sala arranca al iniciarla.
    public class RelojSalaTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();
        private readonly CronometroQueRegistra _cronometro = new();

        [Fact]
        public async Task CrearSala_GuardaLaDuracionYNoArrancaElReloj()
        {
            var evaluacionId = Guid.NewGuid();
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId, "Publicada", Array.Empty<PreguntaEvaluacionDto>());
            var handler = new CrearSalaCommandHandler(_unitOfWork, _salaRepository, _evaluacionesClient);
            var antes = DateTime.UtcNow;

            var sala = await handler.HandleAsync(new CrearSalaCommand
            {
                InstitucionId = Guid.NewGuid(),
                EvaluacionId = evaluacionId,
                CapacidadMaxima = 30,
                DuracionMinutos = 45
            }, CancellationToken.None);

            Assert.Equal(45, sala.DuracionMinutos);
            Assert.Null(sala.IniciadaUtc);
            Assert.True(sala.FechaVencimiento >= antes.Add(Sala.EsperaMaximaSinIniciar).AddSeconds(-5));
        }

        [Fact]
        public async Task IniciarSala_CalculaElVencimientoDesdeElInicioYActualizaElCronometro()
        {
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var sala = new Sala
            {
                Id = Guid.NewGuid(),
                InstitucionId = institucionId,
                EvaluacionId = evaluacionId,
                CodigoPIN = 123456,
                Estado = "Activa",
                CapacidadMaxima = 30,
                DuracionMinutos = 45,
                FechaVencimiento = DateTime.UtcNow.Add(Sala.EsperaMaximaSinIniciar)
            };
            _salaRepository.Salas.Add(sala);
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                new[] { new PreguntaEvaluacionDto(Guid.NewGuid(), "¿Qué es un sismo?", "Abierta", 5, null) });
            var handler = new IniciarSalaCommandHandler(_salaRepository, _unitOfWork, _evaluacionesClient, _cronometro);
            var antes = DateTime.UtcNow;

            var resultado = await handler.Handle(new IniciarSalaCommand(sala.Id, institucionId), CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(sala.IniciadaUtc);
            Assert.InRange(sala.FechaVencimiento, antes.AddMinutes(45).AddSeconds(-5), DateTime.UtcNow.AddMinutes(45).AddSeconds(5));
            Assert.Equal(sala.FechaVencimiento, resultado.Valor!.SalaIniciada.FechaLimiteUtc);
            Assert.Equal(sala.IniciadaUtc, resultado.Valor.SalaIniciada.IniciadaUtc);
            Assert.Equal(sala.FechaVencimiento, _cronometro.Vencimientos[sala.Id]);
        }

        [Fact]
        public async Task IniciarSala_VencidaSinIniciar_NoSeInicia()
        {
            var institucionId = Guid.NewGuid();
            var sala = new Sala
            {
                Id = Guid.NewGuid(),
                InstitucionId = institucionId,
                EvaluacionId = Guid.NewGuid(),
                CodigoPIN = 654321,
                Estado = "Activa",
                CapacidadMaxima = 30,
                DuracionMinutos = 45,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(-1)
            };
            _salaRepository.Salas.Add(sala);
            var handler = new IniciarSalaCommandHandler(_salaRepository, _unitOfWork, _evaluacionesClient, _cronometro);

            var resultado = await handler.Handle(new IniciarSalaCommand(sala.Id, institucionId), CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SALA_VENCIDA", resultado.Codigo);
            Assert.Null(sala.IniciadaUtc);
            Assert.Equal("Activa", sala.Estado);
        }

        private sealed class CronometroQueRegistra : ISalaCronometroService
        {
            public Dictionary<Guid, DateTime> Vencimientos { get; } = new();

            public Task EstablecerVencimientoAsync(Guid salaId, DateTime fechaVencimientoUtc, CancellationToken cancellationToken = default)
            {
                Vencimientos[salaId] = fechaVencimientoUtc;
                return Task.CompletedTask;
            }

            public Task<DateTime?> ObtenerVencimientoAsync(Guid salaId, CancellationToken cancellationToken = default) =>
                Task.FromResult<DateTime?>(Vencimientos.TryGetValue(salaId, out var v) ? v : null);

            public Task<bool> EstaVencidaAsync(Guid salaId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
        }
    }
}
