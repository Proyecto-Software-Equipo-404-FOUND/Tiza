using System.Text.Json;
using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class IniciarSalaCommandHandler
        : IRequestHandler<IniciarSalaCommand, ResultadoControlSala<ResultadoInicioSalaDto>>
    {
        private readonly ISalaRepository _salaRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEvaluacionesClient _evaluacionesClient;

        public IniciarSalaCommandHandler(
            ISalaRepository salaRepository,
            IUnitOfWork unitOfWork,
            IEvaluacionesClient evaluacionesClient)
        {
            _salaRepository = salaRepository;
            _unitOfWork = unitOfWork;
            _evaluacionesClient = evaluacionesClient;
        }

        public async Task<ResultadoControlSala<ResultadoInicioSalaDto>> Handle(
            IniciarSalaCommand command,
            CancellationToken cancellationToken)
        {
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);

            if (sala == null)
            {
                return ResultadoControlSala<ResultadoInicioSalaDto>.Error(
                    "SALA_NO_ENCONTRADA",
                    "La sala no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoControlSala<ResultadoInicioSalaDto>.Error(
                    "NO_AUTORIZADO",
                    "No tienes permiso para iniciar esta sala.");
            }

            if (sala.Estado == "Finalizada")
            {
                return ResultadoControlSala<ResultadoInicioSalaDto>.Error(
                    "SALA_FINALIZADA",
                    "La sala ya finalizó.");
            }

            var evaluacion = await ObtenerEvaluacionAsync(sala.EvaluacionId, sala.InstitucionId, cancellationToken);

            if (evaluacion == null || evaluacion.Preguntas.Count == 0)
            {
                return ResultadoControlSala<ResultadoInicioSalaDto>.Error(
                    "SIN_PREGUNTAS",
                    "La evaluación publicada no está disponible o no tiene preguntas.");
            }

            var preguntas = evaluacion.Preguntas;
            var yaIniciada = sala.Estado == "EnCurso";
            var indice = ObtenerIndiceVigente(sala.PreguntaActualId, sala.IndicePreguntaActual, preguntas);

            if (!yaIniciada)
            {
                sala.Iniciar();
            }

            if (!yaIniciada || sala.PreguntaActualId == null || sala.IndicePreguntaActual != indice)
            {
                sala.EstablecerPreguntaActual(preguntas[indice - 1].Id, indice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var iniciadaUtc = DateTime.UtcNow;
            var preguntaActual = PreguntaActualMapper.Crear(
                sala.Id,
                preguntas,
                indice,
                sala.FechaVencimiento);

            return ResultadoControlSala<ResultadoInicioSalaDto>.Ok(
                new ResultadoInicioSalaDto(
                    new SalaIniciadaDto(
                        sala.Id,
                        iniciadaUtc,
                        preguntas.Count,
                        sala.FechaVencimiento),
                    preguntaActual));
        }

        private async Task<EvaluacionPreguntasDto?> ObtenerEvaluacionAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken)
        {
            try
            {
                var evaluacion = await _evaluacionesClient.ObtenerPreguntasAsync(
                    evaluacionId,
                    institucionId,
                    cancellationToken);

                if (evaluacion == null ||
                    !string.Equals(evaluacion.Estado, "Publicada", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return evaluacion;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return null;
            }
        }

        private static int ObtenerIndiceVigente(
            Guid? preguntaActualId,
            int indiceActual,
            IReadOnlyList<PreguntaEvaluacionDto> preguntas)
        {
            if (preguntaActualId.HasValue)
            {
                for (var i = 0; i < preguntas.Count; i++)
                {
                    if (preguntas[i].Id == preguntaActualId.Value)
                    {
                        return i + 1;
                    }
                }
            }

            if (indiceActual >= 1 && indiceActual <= preguntas.Count)
            {
                return indiceActual;
            }

            return 1;
        }
    }
}
