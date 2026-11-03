using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class AvanzarPreguntaCommandHandlerTests
    {
        private readonly FakeSalaRepository _salaRepository = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeEvaluacionesClient _evaluacionesClient = new();

        private AvanzarPreguntaCommandHandler CrearHandler() =>
            new(_salaRepository, _unitOfWork, _evaluacionesClient);

        private Sala CrearSalaEnCurso(
            Guid salaId,
            Guid institucionId,
            Guid evaluacionId,
            params PreguntaEvaluacionDto[] preguntas)
        {
            var sala = new Sala
            {
                Id = salaId,
                InstitucionId = institucionId,
                EvaluacionId = evaluacionId,
                CodigoPIN = 123456,
                Estado = "EnCurso",
                CapacidadMaxima = 50,
                FechaVencimiento = DateTime.UtcNow.AddMinutes(30)
            };

            if (preguntas.Length > 0)
            {
                sala.EstablecerPreguntaActual(preguntas[0].Id, 1);
            }

            _salaRepository.Salas.Add(sala);
            _evaluacionesClient.Evaluaciones[evaluacionId] = new EvaluacionPreguntasDto(
                evaluacionId,
                "Publicada",
                preguntas);

            return sala;
        }

        private static PreguntaEvaluacionDto Pregunta(int numero, string tipo = "Abierta") =>
            new(
                Guid.Parse($"00000000-0000-0000-0000-{numero:D12}"),
                $"Enunciado {numero}",
                tipo,
                4,
                null);

        [Fact]
        public async Task Handle_SinPreguntaIndicada_AvanzaALaSiguiente()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var primera = Pregunta(1);
            var segunda = Pregunta(2);
            var sala = CrearSalaEnCurso(salaId, institucionId, evaluacionId, primera, segunda);

            var resultado = await CrearHandler().Handle(
                new AvanzarPreguntaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.Equal(segunda.Id, resultado.Valor!.PreguntaId);
            Assert.Equal(2, resultado.Valor.Indice);
            Assert.Equal(2, sala.IndicePreguntaActual);
            Assert.Equal(segunda.Id, sala.PreguntaActualId);
        }

        [Fact]
        public async Task Handle_PreguntaIndicada_PublicaEsaPregunta()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            var primera = Pregunta(1);
            var segunda = Pregunta(2);
            var tercera = Pregunta(3);
            CrearSalaEnCurso(salaId, institucionId, evaluacionId, primera, segunda, tercera);

            var resultado = await CrearHandler().Handle(
                new AvanzarPreguntaCommand(salaId, institucionId, tercera.Id),
                CancellationToken.None);

            Assert.True(resultado.Exito);
            Assert.Equal(tercera.Id, resultado.Valor!.PreguntaId);
            Assert.Equal(3, resultado.Valor.Indice);
        }

        [Fact]
        public async Task Handle_PreguntaInexistente_DevuelveError()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            CrearSalaEnCurso(salaId, institucionId, evaluacionId, Pregunta(1));

            var resultado = await CrearHandler().Handle(
                new AvanzarPreguntaCommand(salaId, institucionId, Guid.NewGuid()),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("PREGUNTA_INEXISTENTE", resultado.Codigo);
        }

        [Fact]
        public async Task Handle_SinMasPreguntas_DevuelveError()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var evaluacionId = Guid.NewGuid();
            CrearSalaEnCurso(salaId, institucionId, evaluacionId, Pregunta(1));

            var resultado = await CrearHandler().Handle(
                new AvanzarPreguntaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("PREGUNTA_INEXISTENTE", resultado.Codigo);
        }

        [Fact]
        public async Task Handle_SalaNoIniciada_DevuelveSalaNoIniciada()
        {
            var salaId = Guid.NewGuid();
            var institucionId = Guid.NewGuid();
            var sala = CrearSalaEnCurso(salaId, institucionId, Guid.NewGuid(), Pregunta(1));
            sala.Estado = "Activa";

            var resultado = await CrearHandler().Handle(
                new AvanzarPreguntaCommand(salaId, institucionId),
                CancellationToken.None);

            Assert.False(resultado.Exito);
            Assert.Equal("SALA_NO_INICIADA", resultado.Codigo);
        }
    }
}
