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
        private readonly IEvaluacionesClient? _evaluacionesClient;

        public CrearSalaCommandHandler(
            IUnitOfWork unitOfWork,
            ISalaRepository salaRepository,
            IEvaluacionesClient? evaluacionesClient = null)
        {   
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
            _evaluacionesClient = evaluacionesClient;
        }

        public async Task<Sala> HandleAsync(CrearSalaCommand command, CancellationToken cancellationToken)
        {
            if (command.CapacidadMaxima < 1 || command.CapacidadMaxima > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(command.CapacidadMaxima),
                    "La capacidad máxima debe estar entre 1 y 100.");
            }

            if (_evaluacionesClient != null)
            {
                var evaluacion = await _evaluacionesClient.ObtenerPreguntasAsync(
                    command.EvaluacionId,
                    command.InstitucionId,
                    cancellationToken);

                if (evaluacion == null || !string.Equals(evaluacion.Estado, "Publicada", StringComparison.OrdinalIgnoreCase))
                {
                    throw new KeyNotFoundException(
                        "La evaluación no existe, no está publicada o pertenece a otra institución.");
                }
            }

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
