using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class FinalizarSalaCommandHandler
        : IRequestHandler<FinalizarSalaCommand, ResultadoControlSala<FinalizacionSalaDto>>
    {
        private const string MotivoDocente = "Docente";
        private const string MotivoTiempoAgotado = "TiempoAgotado";

        private readonly ISalaRepository _salaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public FinalizarSalaCommandHandler(
            ISalaRepository salaRepository,
            IUnitOfWork unitOfWork)
        {
            _salaRepository = salaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ResultadoControlSala<FinalizacionSalaDto>> Handle(
            FinalizarSalaCommand command,
            CancellationToken cancellationToken)
        {
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);

            if (sala == null)
            {
                return ResultadoControlSala<FinalizacionSalaDto>.Error(
                    "SALA_NO_ENCONTRADA",
                    "La sala no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoControlSala<FinalizacionSalaDto>.Error(
                    "NO_AUTORIZADO",
                    "No tienes permiso para finalizar esta sala.");
            }

            var motivo = NormalizarMotivo(command.Motivo);
            var yaEstabaFinalizada = string.Equals(
                sala.Estado,
                "Finalizada",
                StringComparison.OrdinalIgnoreCase);

            var transiciono = sala.Finalizar(motivo);

            if (transiciono)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return ResultadoControlSala<FinalizacionSalaDto>.Ok(
                new FinalizacionSalaDto(
                    sala.Id,
                    sala.FinalizadaUtc ?? DateTime.UtcNow,
                    sala.MotivoCierre ?? motivo,
                    yaEstabaFinalizada));
        }

        private static string NormalizarMotivo(string? motivo)
        {
            return string.Equals(
                motivo,
                MotivoTiempoAgotado,
                StringComparison.OrdinalIgnoreCase)
                ? MotivoTiempoAgotado
                : MotivoDocente;
        }
    }
}
