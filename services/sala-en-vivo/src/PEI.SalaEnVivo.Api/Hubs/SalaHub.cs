using System.Collections.Concurrent;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PEI.SalaEnVivo.Application.DTOs.Foco;
using PEI.SalaEnVivo.Application.DTOs.Respuestas;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Queries;

namespace PEI.SalaEnVivo.Api.Hubs
{
    [Authorize]
    public sealed class SalaHub : Hub
    {
        private const string RolDocente = "Docente";
        private static readonly ConcurrentDictionary<(Guid SalaId, Guid AlumnoId), string> _conexionesActivasPorAlumno = new();
        private readonly ISender _sender;

        public SalaHub(ISender sender)
        {
            _sender = sender;
        }

        public async Task UnirseASala(int pin)
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var alumnoId))
            {
                throw new HubException("Alumno no identificado.");
            }

            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            if (!Guid.TryParse(institucionClaim, out var institucionId))
            {
                throw new HubException("Institución no válida.");
            }

            try
            {
                var salaId = await _sender.Send(
                    new UnirseASalaPorPINCommand(
                        pin,
                        alumnoId,
                        institucionId),
                    Context.ConnectionAborted);

                Context.Items["SalaId"] = salaId;

                await VincularSesionUnicaAlumnoAsync(salaId, alumnoId);

                var estado = await _sender.Send(
                    new ObtenerEstadoSalaQuery(
                        alumnoId,
                        institucionId,
                        "Alumno",
                        salaId),
                    Context.ConnectionAborted);

                await Clients.Caller.SendAsync(
                    "estadoSala",
                    estado,
                    Context.ConnectionAborted);

                await NotificarIngresoAlumnoAsync(salaId, alumnoId);
            }
            catch (UnauthorizedAccessException)
            {
                throw new HubException(
                    "No tienes permiso para ingresar a esta sala.");
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        public async Task<EstadoSalaDto> ObtenerEstadoSala()
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var usuarioId))
            {
                throw new HubException("NO_AUTORIZADO: Usuario no identificado.");
            }

            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            if (!Guid.TryParse(institucionClaim, out var institucionId))
            {
                throw new HubException("NO_AUTORIZADO: Institución no válida.");
            }

            var rol = user?.FindFirst(ClaimTypes.Role)?.Value
                      ?? user?.FindFirst("role")?.Value
                      ?? "Alumno";

            Guid? salaIdContexto = null;
            if (Context.Items.TryGetValue("SalaId", out var salaIdObj) && salaIdObj is Guid id && id != Guid.Empty)
            {
                salaIdContexto = id;
            }

            try
            {
                var estado = await _sender.Send(
                    new ObtenerEstadoSalaQuery(
                        usuarioId,
                        institucionId,
                        rol,
                        salaIdContexto),
                    Context.ConnectionAborted);

                Context.Items["SalaId"] = estado.SalaId;

                var esDocente = string.Equals(rol, RolDocente, StringComparison.OrdinalIgnoreCase);
                if (esDocente)
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        estado.SalaId.ToString(),
                        Context.ConnectionAborted);
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        $"{estado.SalaId}:docentes",
                        Context.ConnectionAborted);
                }
                else
                {
                    await VincularSesionUnicaAlumnoAsync(estado.SalaId, usuarioId);
                }

                await Clients.Caller.SendAsync(
                    "estadoSala",
                    estado,
                    Context.ConnectionAborted);

                return estado;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        public async Task UnirseASalaDocente(Guid salaId)
        {
            if (!EsDocente())
            {
                throw new HubException(
                    "NO_AUTORIZADO: Solo un docente puede unirse como docente.");
            }

            var institucionId = ObtenerInstitucionId();

            var resultado = await _sender.Send(
                new UnirseASalaDocenteCommand(salaId, institucionId),
                Context.ConnectionAborted);

            if (!resultado.Exito)
            {
                throw new HubException($"{resultado.Codigo}: {resultado.Mensaje}");
            }

            var salaIdValidada = resultado.Valor;

            Context.Items["SalaId"] = salaIdValidada;
            Context.Items["Rol"] = RolDocente;

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                salaIdValidada.ToString(),
                Context.ConnectionAborted);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"{salaIdValidada}:docentes",
                Context.ConnectionAborted);
        }

        public async Task IniciarSala()
        {
            var (salaId, institucionId) = ObtenerContextoDocente();

            var resultado = await _sender.Send(
                new IniciarSalaCommand(salaId, institucionId),
                Context.ConnectionAborted);

            if (!resultado.Exito || resultado.Valor == null)
            {
                throw new HubException($"{resultado.Codigo}: {resultado.Mensaje}");
            }

            var valor = resultado.Valor;
            var grupoSala = valor.SalaIniciada.SalaId.ToString();

            await Clients.Group(grupoSala).SendAsync(
                "salaIniciada",
                new
                {
                    salaId = valor.SalaIniciada.SalaId,
                    iniciadaUtc = valor.SalaIniciada.IniciadaUtc,
                    totalPreguntas = valor.SalaIniciada.TotalPreguntas,
                    fechaLimiteUtc = valor.SalaIniciada.FechaLimiteUtc
                },
                Context.ConnectionAborted);

            await Clients.Group(grupoSala).SendAsync(
                "preguntaActual",
                MapearPreguntaActual(valor.PreguntaActual),
                Context.ConnectionAborted);
        }

        public async Task AvanzarPregunta(Guid? preguntaId = null)
        {
            var (salaId, institucionId) = ObtenerContextoDocente();

            var resultado = await _sender.Send(
                new AvanzarPreguntaCommand(salaId, institucionId, preguntaId),
                Context.ConnectionAborted);

            if (!resultado.Exito || resultado.Valor == null)
            {
                throw new HubException($"{resultado.Codigo}: {resultado.Mensaje}");
            }

            await Clients.Group(resultado.Valor.SalaId.ToString()).SendAsync(
                "preguntaActual",
                MapearPreguntaActual(resultado.Valor),
                Context.ConnectionAborted);
        }

        public async Task<ResultadoEnvioRespuestaDto> EnviarRespuesta(EnviarRespuestaRequest request)
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var alumnoId))
            {
                throw new HubException("Alumno no identificado.");
            }

            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            _ = Guid.TryParse(institucionClaim, out var institucionId);

            // Obtener SalaId del contexto de la conexión o resolver desde base de datos
            if (!Context.Items.TryGetValue("SalaId", out var salaIdObj) || !(salaIdObj is Guid salaId))
            {
                var participanteRepo = Context.GetHttpContext()?.RequestServices.GetService<IParticipanteSalaRepository>();
                var salaIdRecuperada = participanteRepo != null
                    ? await participanteRepo.ObtenerUltimaSalaIdDeAlumnoAsync(alumnoId, Context.ConnectionAborted)
                    : null;

                if (!salaIdRecuperada.HasValue)
                {
                    return ResultadoEnvioRespuestaDto.Rechazada(
                        request.PreguntaId, request.IdempotencyKey, request.Secuencia,
                        "ALUMNO_NO_EN_SALA", "El alumno no se encuentra unido a ninguna sala activa.",
                        reintentable: false, correlationId: Context.ConnectionId);
                }

                salaId = salaIdRecuperada.Value;
                Context.Items["SalaId"] = salaId;
            }

            // Verificar que esta sesión no haya sido desplazada por una reconexión en otro dispositivo/pestaña
            if (_conexionesActivasPorAlumno.TryGetValue((salaId, alumnoId), out var conexionActiva)
                && !string.Equals(conexionActiva, Context.ConnectionId, StringComparison.Ordinal))
            {
                return ResultadoEnvioRespuestaDto.Rechazada(
                    request.PreguntaId, request.IdempotencyKey, request.Secuencia,
                    "SESION_REEMPLAZADA", "Tu sesión fue reemplazada por una conexión más reciente en la sala.",
                    reintentable: false, correlationId: Context.ConnectionId);
            }

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                request.PreguntaId,
                request.Respuesta,
                request.IdempotencyKey,
                request.Secuencia,
                Context.ConnectionId);

            var resultado = await _sender.Send(command, Context.ConnectionAborted);

            // Si el envío fue aceptado, notificar en tiempo real al grupo de docentes
            if (resultado.Estado == "Aceptada")
            {
                await Clients.Group($"{salaId}:docentes").SendAsync(
                    "respuestaRegistrada",
                    new
                    {
                        salaId,
                        alumnoId,
                        preguntaId = request.PreguntaId,
                        recibidaUtc = resultado.RecibidaUtc
                    },
                    Context.ConnectionAborted);
            }

            return resultado;
        }

        public async Task<EventoFocoRegistradoDto> RegistrarEventoFoco(RegistrarEventoFocoRequest request)
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var alumnoId))
            {
                throw new HubException("NO_AUTORIZADO: Alumno no identificado.");
            }

            var institucionId = ObtenerInstitucionId();

            if (!Context.Items.TryGetValue("SalaId", out var salaIdObj) || salaIdObj is not Guid salaId || salaId == Guid.Empty)
            {
                var participanteRepo = Context.GetHttpContext()?.RequestServices.GetService<IParticipanteSalaRepository>();
                var salaIdRecuperada = participanteRepo != null
                    ? await participanteRepo.ObtenerUltimaSalaIdDeAlumnoAsync(alumnoId, Context.ConnectionAborted)
                    : null;

                if (!salaIdRecuperada.HasValue || salaIdRecuperada.Value == Guid.Empty)
                {
                    throw new HubException("ALUMNO_NO_EN_SALA: El alumno no se encuentra unido a ninguna sala activa.");
                }

                salaId = salaIdRecuperada.Value;
                Context.Items["SalaId"] = salaId;
            }

            try
            {
                var evento = await _sender.Send(
                    new RegistrarEventoFocoCommand(
                        salaId,
                        alumnoId,
                        institucionId,
                        request.DuracionMs,
                        request.TipoEvento,
                        request.PreguntaId),
                    Context.ConnectionAborted);

                await Clients.Group($"{salaId}:docentes").SendAsync(
                    "alertaFoco",
                    new
                    {
                        id = evento.Id,
                        salaId = evento.SalaId,
                        alumnoId = evento.AlumnoId,
                        preguntaId = evento.PreguntaId,
                        tipoEvento = evento.TipoEvento,
                        duracionMs = evento.DuracionMs,
                        severidad = evento.Severidad,
                        registradoUtc = evento.RegistradoUtc
                    },
                    Context.ConnectionAborted);

                return evento;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        private async Task VincularSesionUnicaAlumnoAsync(Guid salaId, Guid alumnoId)
        {
            var clave = (salaId, alumnoId);
            var nuevaConexionId = Context.ConnectionId;

            if (_conexionesActivasPorAlumno.TryGetValue(clave, out var conexionPrevia)
                && !string.Equals(conexionPrevia, nuevaConexionId, StringComparison.Ordinal))
            {
                await Groups.RemoveFromGroupAsync(
                    conexionPrevia,
                    salaId.ToString(),
                    Context.ConnectionAborted);
            }

            _conexionesActivasPorAlumno[clave] = nuevaConexionId;

            await Groups.AddToGroupAsync(
                nuevaConexionId,
                salaId.ToString(),
                Context.ConnectionAborted);
        }

        private async Task NotificarIngresoAlumnoAsync(Guid salaId, Guid alumnoId)
        {
            var participanteRepo = Context.GetHttpContext()?
                .RequestServices.GetService<IParticipanteSalaRepository>();

            var totalConectados = participanteRepo != null
                ? await participanteRepo.ContarAlumnosAsync(salaId, Context.ConnectionAborted)
                : 0;

            if (totalConectados < 1)
            {
                totalConectados = 1;
            }

            await Clients.Group($"{salaId}:docentes").SendAsync(
                "alumnoIngreso",
                new
                {
                    salaId,
                    alumnoId,
                    alias = $"Alumno {totalConectados}",
                    totalConectados,
                    ingresoUtc = DateTime.UtcNow
                },
                Context.ConnectionAborted);
        }

        private (Guid SalaId, Guid InstitucionId) ObtenerContextoDocente()
        {
            if (!EsDocente())
            {
                throw new HubException(
                    "NO_AUTORIZADO: Solo un docente puede realizar esta acción.");
            }

            var institucionId = ObtenerInstitucionId();

            if (!Context.Items.TryGetValue("SalaId", out var salaIdObj) ||
                salaIdObj is not Guid salaId)
            {
                throw new HubException(
                    "NO_AUTORIZADO: El docente no está unido a una sala.");
            }

            return (salaId, institucionId);
        }

        private Guid ObtenerInstitucionId()
        {
            var user = Context.User;
            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            if (!Guid.TryParse(institucionClaim, out var institucionId))
            {
                throw new HubException("NO_AUTORIZADO: Institución no válida.");
            }

            return institucionId;
        }

        private bool EsDocente()
        {
            var user = Context.User;

            return user?.IsInRole(RolDocente) == true
                   || user?.FindFirst("role")?.Value == RolDocente
                   || user?.FindFirst(ClaimTypes.Role)?.Value == RolDocente;
        }

        private static object MapearPreguntaActual(PreguntaActualDto pregunta)
        {
            return new
            {
                salaId = pregunta.SalaId,
                preguntaId = pregunta.PreguntaId,
                indice = pregunta.Indice,
                totalPreguntas = pregunta.TotalPreguntas,
                tipo = pregunta.Tipo,
                enunciado = pregunta.Enunciado,
                opciones = pregunta.Opciones?
                    .Select(o => new { id = o.Id, texto = o.Texto })
                    .ToArray(),
                puntajeMaximo = pregunta.PuntajeMaximo,
                fechaInicioUtc = pregunta.FechaInicioUtc,
                fechaLimiteUtc = pregunta.FechaLimiteUtc
            };
        }
    }
}
