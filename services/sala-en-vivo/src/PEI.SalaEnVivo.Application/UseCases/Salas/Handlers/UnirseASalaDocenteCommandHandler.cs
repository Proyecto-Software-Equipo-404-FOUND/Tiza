using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class UnirseASalaDocenteCommandHandler
        : IRequestHandler<UnirseASalaDocenteCommand, ResultadoControlSala<Guid>>
    {
        private readonly ISalaRepository _salaRepository;

        public UnirseASalaDocenteCommandHandler(ISalaRepository salaRepository)
        {
            _salaRepository = salaRepository;
        }

        public async Task<ResultadoControlSala<Guid>> Handle(
            UnirseASalaDocenteCommand command,
            CancellationToken cancellationToken)
        {
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);

            if (sala == null)
            {
                return ResultadoControlSala<Guid>.Error(
                    "SALA_NO_ENCONTRADA",
                    "La sala no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoControlSala<Guid>.Error(
                    "NO_AUTORIZADO",
                    "No tienes permiso para ingresar a esta sala.");
            }

            if (sala.Estado == "Finalizada")
            {
                return ResultadoControlSala<Guid>.Error(
                    "SALA_FINALIZADA",
                    "La sala ya finalizó.");
            }

            return ResultadoControlSala<Guid>.Ok(sala.Id);
        }
    }
}
