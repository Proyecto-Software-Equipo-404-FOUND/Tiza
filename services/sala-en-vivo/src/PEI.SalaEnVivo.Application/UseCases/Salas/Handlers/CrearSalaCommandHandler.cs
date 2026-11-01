using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using System.Security.Cryptography;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public class CrearSalaCommandHandler
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISalaRepository _salaRepository;

        public CrearSalaCommandHandler(IUnitOfWork unitOfWork, ISalaRepository salaRepository)
        {   
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
        }

        public async Task<Sala> HandleAsync(CrearSalaCommand command, CancellationToken cancellationToken)
        {
            var sala = new Sala
            {
                InstitucionId = command.InstitucionId,
                EvaluacionId = command.EvaluacionId,
                CodigoPIN = await CrearPIN(cancellationToken),
                FechaVencimiento = DateTime.UtcNow.AddHours(2),
                Estado = "Activa",
            };
            sala.SetCapacidadMaxima(command.CapacidadMaxima);

            await _salaRepository.AddAsync(sala, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return sala;
        }

        private async Task<int> CrearPIN(CancellationToken cancellationToken)
        {
            for (int intentos = 0; intentos < 30; intentos++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int pin = RandomNumberGenerator.GetInt32(100000, 1000000);

                if (!await _salaRepository.ExisteActivaPorPINAsync(pin, cancellationToken))
                {
                    return pin;
                }
            }

            throw new InvalidOperationException("No se pudo generar un PIN disponible después de 30 intentos.");
        }
    }
}
