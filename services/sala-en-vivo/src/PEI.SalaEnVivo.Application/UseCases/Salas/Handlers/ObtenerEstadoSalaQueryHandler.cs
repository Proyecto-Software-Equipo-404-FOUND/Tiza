using System.Text.Json;
using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Queries;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class ObtenerEstadoSalaQueryHandler : IRequestHandler<ObtenerEstadoSalaQuery, EstadoSalaDto>
    {
        private readonly ISalaRepository _salaRepository;
        private readonly IParticipanteSalaRepository _participanteRepository;
        private readonly IRespuestaSalaRepository _respuestaRepository;
        private readonly ISalaCronometroService _cronometroService;
        private readonly IEvaluacionesClient? _evaluacionesClient;

        public ObtenerEstadoSalaQueryHandler(
            ISalaRepository salaRepository,
            IParticipanteSalaRepository participanteRepository,
            IRespuestaSalaRepository respuestaRepository,
            ISalaCronometroService cronometroService,
            IEvaluacionesClient? evaluacionesClient = null)
        {
            _salaRepository = salaRepository;
            _participanteRepository = participanteRepository;
            _respuestaRepository = respuestaRepository;
            _cronometroService = cronometroService;
            _evaluacionesClient = evaluacionesClient;
        }

        public async Task<EstadoSalaDto> Handle(
            ObtenerEstadoSalaQuery request,
            CancellationToken cancellationToken)
        {
            if (request.UsuarioId == Guid.Empty || request.InstitucionId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("NO_AUTORIZADO: Usuario o institución no válidos.");
            }

            var esDocente = string.Equals(request.Rol, "Docente", StringComparison.OrdinalIgnoreCase);
            Guid salaId;

            if (request.SalaId.HasValue && request.SalaId.Value != Guid.Empty)
            {
                salaId = request.SalaId.Value;
            }
            else
            {
                if (esDocente)
                {
                    throw new InvalidOperationException("SALA_NO_ENCONTRADA: El docente no tiene una sala asociada a la conexión actual.");
                }

                var salaRecuperada = await _participanteRepository.ObtenerUltimaSalaIdDeAlumnoAsync(
                    request.UsuarioId,
                    cancellationToken);

                if (!salaRecuperada.HasValue || salaRecuperada.Value == Guid.Empty)
                {
                    throw new InvalidOperationException("ALUMNO_NO_EN_SALA: El alumno no se encuentra registrado en ninguna sala.");
                }

                salaId = salaRecuperada.Value;
            }

            var sala = await _salaRepository.ObtenerPorIdAsync(salaId, cancellationToken);
            if (sala == null)
            {
                throw new KeyNotFoundException("SALA_NO_ENCONTRADA: La sala solicitada no existe.");
            }

            if (sala.InstitucionId != request.InstitucionId)
            {
                throw new UnauthorizedAccessException("SALA_OTRA_INSTITUCION: No tienes permiso para acceder a una sala de otra institución.");
            }

            int ultimaSecuencia = 0;
            IReadOnlyList<Guid> preguntasRespondidas = Array.Empty<Guid>();

            if (!esDocente)
            {
                if (request.SalaId.HasValue && request.SalaId.Value != Guid.Empty)
                {
                    var perteneceASala = await _participanteRepository.ExisteAlumnoEnSalaAsync(
                        salaId,
                        request.UsuarioId,
                        cancellationToken);

                    if (!perteneceASala)
                    {
                        throw new InvalidOperationException("ALUMNO_NO_EN_SALA: El alumno no está registrado en esta sala.");
                    }
                }

                ultimaSecuencia = await _respuestaRepository.ObtenerUltimaSecuenciaAsync(
                    salaId,
                    request.UsuarioId,
                    cancellationToken);

                preguntasRespondidas = await _respuestaRepository.ObtenerPreguntasRespondidasAsync(
                    salaId,
                    request.UsuarioId,
                    cancellationToken);
            }

            var vencimientoCronometro = await _cronometroService.ObtenerVencimientoAsync(salaId, cancellationToken);
            DateTime? fechaLimiteUtc = vencimientoCronometro
                ?? (sala.FechaVencimiento != default ? DateTime.SpecifyKind(sala.FechaVencimiento, DateTimeKind.Utc) : null);

            PreguntaActualDto? preguntaActual = null;
            if (_evaluacionesClient != null &&
                string.Equals(sala.Estado, "EnCurso", StringComparison.OrdinalIgnoreCase))
            {
                var evaluacion = await ObtenerEvaluacionAsync(sala.EvaluacionId, sala.InstitucionId, cancellationToken);
                if (evaluacion != null && evaluacion.Preguntas.Count > 0)
                {
                    var indice = ObtenerIndiceVigente(sala.PreguntaActualId, sala.IndicePreguntaActual, evaluacion.Preguntas);
                    preguntaActual = PreguntaActualMapper.Crear(
                        sala.Id,
                        evaluacion.Preguntas,
                        indice,
                        fechaLimiteUtc ?? sala.FechaVencimiento);
                }
            }

            return new EstadoSalaDto(
                SalaId: sala.Id,
                Estado: sala.Estado,
                PreguntaActual: preguntaActual,
                UltimaSecuencia: ultimaSecuencia,
                PreguntasRespondidas: preguntasRespondidas,
                ServidorUtc: DateTime.UtcNow,
                FechaLimiteUtc: fechaLimiteUtc);
        }

        private async Task<EvaluacionPreguntasDto?> ObtenerEvaluacionAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken)
        {
            if (_evaluacionesClient == null || evaluacionId == Guid.Empty)
            {
                return null;
            }

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
