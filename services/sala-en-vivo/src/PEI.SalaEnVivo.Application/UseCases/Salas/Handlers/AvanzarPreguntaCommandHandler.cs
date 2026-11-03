using System.Text.Json;
using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class AvanzarPreguntaCommandHandler
        : IRequestHandler<AvanzarPreguntaCommand, ResultadoControlSala<PreguntaActualDto>>
    {
        private readonly ISalaRepository _salaRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEvaluacionesClient _evaluacionesClient;

        public AvanzarPreguntaCommandHandler(
            ISalaRepository salaRepository,
            IUnitOfWork unitOfWork,
            IEvaluacionesClient evaluacionesClient)
        {
            _salaRepository = salaRepository;
            _unitOfWork = unitOfWork;
            _evaluacionesClient = evaluacionesClient;
        }

        public async Task<ResultadoControlSala<PreguntaActualDto>> Handle(
            AvanzarPreguntaCommand command,
            CancellationToken cancellationToken)
        {
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);

            if (sala == null)
            {
                return ResultadoControlSala<PreguntaActualDto>.Error(
                    "SALA_NO_ENCONTRADA",
                    "La sala no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoControlSala<PreguntaActualDto>.Error(
                    "NO_AUTORIZADO",
                    "No tienes permiso para avanzar la sala.");
            }

            if (sala.Estado == "Finalizada")
            {
                return ResultadoControlSala<PreguntaActualDto>.Error(
                    "SALA_FINALIZADA",
                    "La sala ya finalizó.");
            }

            if (sala.Estado != "EnCurso")
            {
                return ResultadoControlSala<PreguntaActualDto>.Error(
                    "SALA_NO_INICIADA",
                    "La sala todavía no fue iniciada.");
            }

            var evaluacion = await ObtenerEvaluacionAsync(sala.EvaluacionId, sala.InstitucionId, cancellationToken);

            if (evaluacion == null || evaluacion.Preguntas.Count == 0)
            {
                return ResultadoControlSala<PreguntaActualDto>.Error(
                    "SIN_PREGUNTAS",
                    "La evaluación publicada no está disponible o no tiene preguntas.");
            }

            var preguntas = evaluacion.Preguntas;
            int indice;

            if (command.PreguntaId.HasValue)
            {
                indice = BuscarIndice(preguntas, command.PreguntaId.Value);

                if (indice == 0)
                {
                    return ResultadoControlSala<PreguntaActualDto>.Error(
                        "PREGUNTA_INEXISTENTE",
                        "La pregunta indicada no pertenece a la evaluación publicada.");
                }

                if (sala.PreguntaActualId == command.PreguntaId &&
                    sala.IndicePreguntaActual == indice)
                {
                    return ResultadoControlSala<PreguntaActualDto>.Ok(
                        PreguntaActualMapper.Crear(sala.Id, preguntas, indice, sala.FechaVencimiento));
                }
            }
            else
            {
                indice = sala.IndicePreguntaActual + 1;

                if (indice > preguntas.Count)
                {
                    return ResultadoControlSala<PreguntaActualDto>.Error(
                        "PREGUNTA_INEXISTENTE",
                        "No hay más preguntas para avanzar.");
                }
            }

            sala.EstablecerPreguntaActual(preguntas[indice - 1].Id, indice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ResultadoControlSala<PreguntaActualDto>.Ok(
                PreguntaActualMapper.Crear(sala.Id, preguntas, indice, sala.FechaVencimiento));
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

        private static int BuscarIndice(
            IReadOnlyList<PreguntaEvaluacionDto> preguntas,
            Guid preguntaId)
        {
            for (var i = 0; i < preguntas.Count; i++)
            {
                if (preguntas[i].Id == preguntaId)
                {
                    return i + 1;
                }
            }

            return 0;
        }
    }
}
