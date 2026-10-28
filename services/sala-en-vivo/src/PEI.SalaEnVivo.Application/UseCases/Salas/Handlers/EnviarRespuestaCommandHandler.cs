using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEI.SalaEnVivo.Application.DTOs.Respuestas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Handlers
{
    public sealed class EnviarRespuestaCommandHandler : IRequestHandler<EnviarRespuestaCommand, ResultadoEnvioRespuestaDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISalaRepository _salaRepository;
        private readonly IRespuestaSalaRepository _respuestaRepository;
        private readonly ISalaCronometroService _cronometroService;
        private readonly ILogger<EnviarRespuestaCommandHandler> _logger;

        public EnviarRespuestaCommandHandler(
            IUnitOfWork unitOfWork,
            ISalaRepository salaRepository,
            IRespuestaSalaRepository respuestaRepository,
            ISalaCronometroService cronometroService,
            ILogger<EnviarRespuestaCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _salaRepository = salaRepository;
            _respuestaRepository = respuestaRepository;
            _cronometroService = cronometroService;
            _logger = logger;
        }

        public async Task<ResultadoEnvioRespuestaDto> Handle(
            EnviarRespuestaCommand command,
            CancellationToken cancellationToken)
        {
            var cid = command.CorrelationId ?? Guid.NewGuid().ToString();

            // 1. Identidad y pertenencia a sala
            if (command.AlumnoId == Guid.Empty || command.SalaId == Guid.Empty)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "ALUMNO_NO_EN_SALA", "La conexión no está vinculada a una sala activa.",
                    reintentable: false, correlationId: cid);
            }

            // 2. Validación de forma y contenido
            if (command.PreguntaId == Guid.Empty || command.IdempotencyKey == Guid.Empty || command.Secuencia < 1)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "PAYLOAD_INVALIDO", "Faltan campos obligatorios o el formato de identificador es inválido.",
                    reintentable: false, correlationId: cid);
            }

            if (string.IsNullOrWhiteSpace(command.Respuesta) || command.Respuesta.Trim().Length > 10000)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "RESPUESTA_INVALIDA", "La respuesta no puede estar vacía ni superar los 10.000 caracteres.",
                    reintentable: false, correlationId: cid);
            }

            var respuestaNormalizada = command.Respuesta.Trim();

            // 3. Verificación de Idempotencia previa (antes de verificar vigencia para respetar reintentos)
            var respuestaPrevia = await _respuestaRepository.ObtenerPorClaveIdempotenciaAsync(
                command.SalaId,
                command.AlumnoId,
                command.IdempotencyKey,
                cancellationToken);

            if (respuestaPrevia != null)
            {
                // Si el payload canónico coincide, responder Duplicada con el timestamp original
                if (respuestaPrevia.PreguntaId == command.PreguntaId &&
                    respuestaPrevia.Respuesta == respuestaNormalizada &&
                    respuestaPrevia.Secuencia == command.Secuencia)
                {
                    _logger.LogInformation(
                        "Envío duplicado detectado para alumno {AlumnoId}, sala {SalaId}, clave {Key}. Devolviendo acuse original.",
                        command.AlumnoId, command.SalaId, command.IdempotencyKey);

                    return ResultadoEnvioRespuestaDto.Duplicada(
                        command.PreguntaId,
                        command.IdempotencyKey,
                        command.Secuencia,
                        respuestaPrevia.RecibidaUtc);
                }

                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "IDEMPOTENCY_KEY_REUTILIZADA", "La clave de idempotencia ya fue utilizada con un payload distinto.",
                    reintentable: false, correlationId: cid);
            }

            // 4. Cronómetro del servidor
            var vencida = await _cronometroService.EstaVencidaAsync(command.SalaId, DateTime.UtcNow, cancellationToken);
            if (vencida)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "TIEMPO_AGOTADO", "El tiempo límite de la sala o de la pregunta ha expirado.",
                    reintentable: false, correlationId: cid);
            }

            // 5. Estado de la sala
            var sala = await _salaRepository.ObtenerPorIdAsync(command.SalaId, cancellationToken);
            if (sala == null)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "SALA_NO_ENCONTRADA", "La sala no existe o no se encuentra disponible.",
                    reintentable: false, correlationId: cid);
            }

            if (command.InstitucionId != Guid.Empty && sala.InstitucionId != command.InstitucionId)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "SALA_OTRA_INSTITUCION", "La sala pertenece a otra institución.",
                    reintentable: false, correlationId: cid);
            }

            if (sala.Estado == "Finalizada")
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "SALA_FINALIZADA", "La sala ya ha sido finalizada y no acepta nuevas respuestas.",
                    reintentable: false, correlationId: cid);
            }

            // 6. Una respuesta por pregunta
            var yaRespondio = await _respuestaRepository.ExisteRespuestaAPreguntaAsync(
                command.SalaId,
                command.AlumnoId,
                command.PreguntaId,
                cancellationToken);

            if (yaRespondio)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "RESPUESTA_YA_REGISTRADA", "Ya existe una respuesta definitiva registrada para esta pregunta.",
                    reintentable: false, correlationId: cid);
            }

            // 7. Secuencia estrictamente incremental
            var ultimaSecuencia = await _respuestaRepository.ObtenerUltimaSecuenciaAsync(
                command.SalaId,
                command.AlumnoId,
                cancellationToken);

            if (command.Secuencia != ultimaSecuencia + 1)
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "SECUENCIA_FUERA_DE_ORDEN", $"La secuencia {command.Secuencia} no es la esperada ({ultimaSecuencia + 1}).",
                    reintentable: true, correlationId: cid,
                    detalles: new { ultimaSecuencia, secuenciaEsperada = ultimaSecuencia + 1 });
            }

            // 8. Persistencia transaccional
            var ahoraUtc = DateTime.UtcNow;
            var nuevaRespuesta = RespuestaSala.Crear(
                sala.InstitucionId,
                command.SalaId,
                command.AlumnoId,
                command.PreguntaId,
                respuestaNormalizada,
                command.IdempotencyKey,
                command.Secuencia,
                ahoraUtc);

            await _respuestaRepository.AddAsync(nuevaRespuesta, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Respuesta aceptada para alumno {AlumnoId}, sala {SalaId}, pregunta {PreguntaId}, secuencia {Secuencia}.",
                    command.AlumnoId, command.SalaId, command.PreguntaId, command.Secuencia);

                return ResultadoEnvioRespuestaDto.Aceptada(
                    command.PreguntaId,
                    command.IdempotencyKey,
                    command.Secuencia,
                    ahoraUtc);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Colisión concurrente al guardar respuesta para alumno {AlumnoId}, sala {SalaId}.", command.AlumnoId, command.SalaId);

                // Re-verificar carrera por clave de idempotencia
                var duplicada = await _respuestaRepository.ObtenerPorClaveIdempotenciaAsync(
                    command.SalaId,
                    command.AlumnoId,
                    command.IdempotencyKey,
                    cancellationToken);

                if (duplicada != null && duplicada.Respuesta == respuestaNormalizada)
                {
                    return ResultadoEnvioRespuestaDto.Duplicada(
                        command.PreguntaId,
                        command.IdempotencyKey,
                        command.Secuencia,
                        duplicada.RecibidaUtc);
                }

                return ResultadoEnvioRespuestaDto.Rechazada(
                    command.PreguntaId, command.IdempotencyKey, command.Secuencia,
                    "RESPUESTA_YA_REGISTRADA", "Se registró otra respuesta concurrentemente.",
                    reintentable: false, correlationId: cid);
            }
        }
    }
}
