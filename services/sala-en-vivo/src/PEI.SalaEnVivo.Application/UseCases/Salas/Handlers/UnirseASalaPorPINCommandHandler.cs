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
        private readonly IEvaluacionesClient? _evaluacionesClient;
        private readonly ICursosClient? _cursosClient;

        public UnirseASalaPorPINCommandHandler(
            IUnitOfWork unitOfWork,
            ISalaRepository salaRepository,
            IParticipanteSalaRepository participanteSalaRepository,
            IEvaluacionesClient? evaluacionesClient = null,
            ICursosClient? cursosClient = null)
        {
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
            _participanteSalaRepository = participanteSalaRepository;
            _evaluacionesClient = evaluacionesClient;
            _cursosClient = cursosClient;
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

                await ValidarMatriculaAsync(sala, command, cancellationToken);

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

        /// <summary>
        /// Solo entra el alumno con matrícula activa en la cohorte a la que se publicó la evaluación.
        /// Si Cursos o Evaluaciones no responden, no se deja entrar (MATRICULA_NO_VERIFICADA).
        /// Los mensajes siguen el formato «CODIGO: mensaje» del contrato del hub.
        /// </summary>
        private async Task ValidarMatriculaAsync(
            Sala sala,
            UnirseASalaPorPINCommand command,
            CancellationToken cancellationToken)
        {
            if (_evaluacionesClient == null || _cursosClient == null)
            {
                return;
            }

            bool matriculado;

            try
            {
                var evaluacion = await _evaluacionesClient.ObtenerPreguntasAsync(
                    sala.EvaluacionId,
                    sala.InstitucionId,
                    cancellationToken);

                if (evaluacion?.CohorteId is not Guid cohorteId)
                {
                    // Una evaluación publicada siempre tiene cohorte; sin cohorte no hay contra qué validar.
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.TokenAlumno))
                {
                    throw new InvalidOperationException(
                        "MATRICULA_NO_VERIFICADA: No pudimos verificar tu matrícula. Volvé a iniciar sesión.");
                }

                matriculado = await _cursosClient.AlumnoTieneMatriculaActivaAsync(
                    cohorteId,
                    command.AlumnoId,
                    command.TokenAlumno,
                    cancellationToken);
            }
            catch (Exception ex) when (
                (ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
                && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "MATRICULA_NO_VERIFICADA: No pudimos verificar tu matrícula. Probá de nuevo en unos segundos.");
            }

            if (!matriculado)
            {
                throw new InvalidOperationException(
                    "ALUMNO_NO_MATRICULADO: No estás matriculado en la cohorte de esta evaluación. Consultá con tu docente.");
            }
        }
    }
}
