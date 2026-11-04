using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class IniciarSalaCommandHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private IniciarSalaCommandHandler CrearHandler() =>
            new(_salaRepository, _unitOfWork, _evaluacionesClient);

        private Sala CrearSala(
            Guid salaId,
            Guid institucionId,
            Guid evaluacionId,
            string estado = "Activa")
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                EvaluacionId = evaluacionId,
                CodigoPIN = 123456,
                Estado = estado,
                CapacidadMaxima = 50,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30)
            };

            _salaRepository.Salas.Add(sala);
            return sala;
        }

        private void RegistrarEvaluacion(Guid evaluacionId, params PreguntaEvaluacionDto[] preguntas)
        {
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                preguntas);
        }

        private static PreguntaEvaluacionDto Pregunta(int numero, string tipo = "OpcionMultiple") =>
            new(
                Guid.Parse($"00000000-0000-0000-0000-{numero:D12}"),
                $"Enunciado {numero}",
                tipo,
                2,
                tipo == "OpcionMultiple"
                    ? new[]
                    {
                        new OpcionEvaluacionDto(
                            Guid.Parse($"10000000-0000-0000-0000-{numero:D12}"),
                            $"Opción {numero}")
                    }
                    : null);

        [Fact]
        public async Task Handle_SalaActivaConPreguntas_IniciaYPublicaLaPrimera()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId, evaluacionId);
            RegistrarEvaluacion(evaluacionId, Pregunta(1), Pregunta(2));

            var resultado = await CrearHandler().Handle(
                new IniciarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.NotNull(resultado.Valor);
            Assert.Equal("EnCurso", sala.Estado);
            Assert.Equal(1, sala.IndicePreguntaActual);
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), sala.PreguntaActualId);
            Assert.Equal(2, resultado.Valor!.SalaIniciada.TotalPreguntas);
            Assert.Equal(1, resultado.Valor.PreguntaActual.Indice);
            Assert.Equal("OpcionMultiple", resultado.Valor.PreguntaActual.Tipo);
        }

        [Fact]
        public async Task Handle_SalaSinPreguntas_DevuelveSinPreguntas()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            CrearSala(salaId, institucionId, evaluacionId);
            RegistrarEvaluacion(evaluacionId);

            var resultado = await CrearHandler().Handle(
                new IniciarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SIN_PREGUNTAS", resultado.Codigo);
        }

        [Fact]
        public async Task Handle_SalaFinalizada_DevuelveSalaFinalizada()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            CrearSala(salaId, institucionId, Guid.NewGuid(), "Finalizada");

            var resultado = await CrearHandler().Handle(
                new IniciarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SALA_FINALIZADA", resultado.Codigo);
        }

        [Fact]
        public async Task Handle_SalaEnCurso_ReemiteLaPreguntaVigente()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var sala = CrearSala(salaId, institucionId, evaluacionId, "EnCurso");
            var segunda = Pregunta(2);
            RegistrarEvaluacion(evaluacionId, Pregunta(1), segunda);
            sala.EstablecerPreguntaActual(segunda.Id, 2);

            var resultado = await CrearHandler().Handle(
                new IniciarSalaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.Equal(segunda.Id, resultado.Valor!.PreguntaActual.PreguntaId);
            Assert.Equal(2, resultado.Valor.PreguntaActual.Indice);
        }
    }

    internal class FakeEvaluacionesClient : IEvaluacionesClient
    {
        public Dictionary<Guid, EvaluacionPreguntasDto> Evaluaciones { get; } = new();

        public Task<EvaluacionPreguntasDto?> ObtenerPreguntasAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<EvaluacionPreguntasDto?>(
                Evaluaciones.TryGetValue(evaluacionId, out var evaluacion)
                    ? evaluacion
                    : null);
        }
    }
}
