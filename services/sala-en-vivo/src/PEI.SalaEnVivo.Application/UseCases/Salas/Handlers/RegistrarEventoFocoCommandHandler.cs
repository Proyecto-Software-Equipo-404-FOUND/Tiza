using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Foco;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class RegistrarEventoFocoCommandHandler : IRequestHandler<RegistrarEventoFocoCommand, EventoFocoRegistradoDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISalaRepository _salaRepository;
        private readonly IParticipanteSalaRepository _participanteRepository;
        private readonly IAuditoriaFocoRepository _auditoriaFocoRepository;

        public RegistrarEventoFocoCommandHandler(
            IUnitOfWork unitOfWork,
            ISalaRepository salaRepository,
            IParticipanteSalaRepository participanteRepository,
            IAuditoriaFocoRepository auditoriaFocoRepository)
        {
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
            _participanteRepository = participanteRepository;
            _auditoriaFocoRepository = auditoriaFocoRepository;
        }

        public async Task<EventoFocoRegistradoDto> Handle(
            RegistrarEventoFocoCommand command,
            CancellationToken cancellationToken)
        {
            if (command.AlumnoId == Guid.Empty || command.InstitucionId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("NO_AUTORIZADO: Usuario o institución no válidos.");
            }

            if (command.SalaId == Guid.Empty)
            {
                throw new InvalidOperationException("ALUMNO_NO_EN_SALA: La conexión no está vinculada a ninguna sala.");
            }

            if (command.DuracionMs < 0)
            {
                throw new InvalidOperationException("PAYLOAD_INVALIDO: La duración fuera de foco no puede ser negativa.");
            }

            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);
            if (sala == null)
            {
                throw new KeyNotFoundException("SALA_NO_ENCONTRADA: La sala solicitada no existe.");
            }

            if (sala.InstitucionId != command.InstitucionId)
            {
                throw new UnauthorizedAccessException("SALA_OTRA_INSTITUCION: No tienes permiso para registrar eventos en una sala de otra institución.");
            }

            if (string.Equals(sala.Estado, "Finalizada", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SALA_FINALIZADA: La sala ya finalizó y no admite nuevos eventos de foco.");
            }

            var perteneceASala = await _participanteRepository.ExisteAlumnoEnSalaAsync(
                command.SalaId,
                command.AlumnoId,
                cancellationToken);

            if (!perteneceASala)
            {
                throw new InvalidOperationException("ALUMNO_NO_EN_SALA: El alumno no está registrado en esta sala.");
            }

            var preguntaAsociada = command.PreguntaId.HasValue && command.PreguntaId.Value != Guid.Empty
                ? command.PreguntaId.Value
                : sala.PreguntaActualId;

            var registro = AuditoriaFoco.Crear(
                sala.InstitucionId,
                sala.Id,
                command.AlumnoId,
                command.DuracionMs,
                command.TipoEvento,
                preguntaAsociada,
                DateTime.UtcNow);

            await _auditoriaFocoRepository.AddAsync(registro, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new EventoFocoRegistradoDto(
                registro.Id,
                registro.SalaId,
                registro.AlumnoId,
                registro.PreguntaId,
                registro.TipoEvento,
                registro.DuracionMs,
                registro.Severidad,
                registro.RegistradoUtc);
        }
    }
}
