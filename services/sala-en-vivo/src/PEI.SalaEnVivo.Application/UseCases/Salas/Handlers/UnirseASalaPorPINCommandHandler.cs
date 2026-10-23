using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using MediatR;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class UnirseASalaPorPINCommandHandler : IRequestHandler<UnirseASalaPorPINCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISalaRepository _salaRepository;
        private readonly IParticipanteSalaRepository _participanteSalaRepository;

        public UnirseASalaPorPINCommandHandler(IUnitOfWork unitOfWork, ISalaRepository salaRepository, IParticipanteSalaRepository participanteSalaRepository)
	    {
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
            _participanteSalaRepository = participanteSalaRepository;
	    }

        public async Task<Guid> Handle(UnirseASalaPorPINCommand command, CancellationToken cancellationToken)
        {
            using var transaccion = await _salaRepository.IniciarTransaccionAsync(cancellationToken);
            try
            {
                var sala = await _salaRepository.ObtenerActivaPorPINAsync(command.PIN, cancellationToken);

                if (sala == null)
                {
                    throw new InvalidOperationException("La sala no existe, está inactiva o venció.");
                }

                if (sala.InstitucionId != command.InstitucionId)
                {
                    throw new UnauthorizedAccessException("La sala pertenece a otra institución.");
                }

                bool existeAlumnoEnSala = await _participanteSalaRepository.ExisteAlumnoEnSalaAsync(sala.Id, command.AlumnoId, cancellationToken);

                if (existeAlumnoEnSala == true)
                {
                    throw new InvalidOperationException("El alumno ya está registrado en la sala.");
                }

                if (sala.CantidadParticipantes >= sala.CapacidadMaxima)
                {
                    throw new InvalidOperationException("La sala ha alcanzado su capacidad máxima.");
                }

                var participante = new ParticipanteSala
                {
                    SalaId = sala.Id,
                    AlumnoId = command.AlumnoId
                };

                await _participanteSalaRepository.AddAsync(participante, cancellationToken);

                sala.CantidadParticipantes++;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await transaccion.CommitAsync(cancellationToken);

                return sala.Id;
            }
            catch (Exception)
            {
                await transaccion.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
