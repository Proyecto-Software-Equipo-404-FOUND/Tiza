using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PEI.SalaEnVivo.Application.DTOs.Foco;
using PEI.SalaEnVivo.Application.DTOs.Presencia;
using PEI.SalaEnVivo.Application.DTOs.Respuestas;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Queries;

namespace PEI.SalaEnVivo.Api.Hubs
{
    [Authorize]
    public sealed class SalaHub : Hub
    {
        private const string RolDocente = "Docente";
        private readonly ISender _sender;
        private readonly IPresenciaSalaService _presencia;
        private readonly ILogger<SalaHub> _logger;

        public SalaHub(
            ISender sender,
            IPresenciaSalaService presencia,
            ILogger<SalaHub> logger)
        {
            _sender = sender;
            _presencia = presencia;
            _logger = logger;
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

                var registroPresencia = await RegistrarPresenciaAlumnoAsync(salaId, alumnoId);

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

                await NotificarIngresoAlumnoAsync(salaId, alumnoId, registroPresencia);
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

            var salaIdEnContexto = salaIdContexto.HasValue;

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
                    Context.Items["Rol"] = RolDocente;

                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        estado.SalaId.ToString(),
                        Context.ConnectionAborted);
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        $"{estado.SalaId}:docentes",
                        Context.ConnectionAborted);

                    await Clients.Caller.SendAsync(
                        "alumnosConectados",
                        await _presencia.ObtenerPresenciaAsync(
                            estado.SalaId,
                            Context.ConnectionAborted),
                        Context.ConnectionAborted);
                }
                else if (!salaIdEnContexto)
                {
                    var registroPresencia = await RegistrarPresenciaAlumnoAsync(
                        estado.SalaId,
                        usuarioId);

                    await NotificarIngresoAlumnoAsync(
                        estado.SalaId,
                        usuarioId,
                        registroPresencia);
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

        public async Task<PresenciaSalaDto> ObtenerAlumnosConectados()
        {
            if (!EsDocente())
            {
                throw new HubException(
                    "NO_AUTORIZADO: Solo un docente puede consultar los alumnos conectados.");
            }

            var (salaId, _) = ObtenerContextoDocente();

            return await _presencia.ObtenerPresenciaAsync(
                salaId,
                Context.ConnectionAborted);
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

            await Clients.Caller.SendAsync(
                "alumnosConectados",
                await _presencia.ObtenerPresenciaAsync(
                    salaIdValidada,
                    Context.ConnectionAborted),
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

        public async Task FinalizarSala(string? motivo = null)
        {
            var (salaId, institucionId) = ObtenerContextoDocente();

            var resultado = await _sender.Send(
                new FinalizarSalaCommand(salaId, institucionId, motivo),
                Context.ConnectionAborted);

            if (!resultado.Exito || resultado.Valor == null)
            {
                throw new HubException($"{resultado.Codigo}: {resultado.Mensaje}");
            }

            var finalizacion = resultado.Valor;

            if (finalizacion.YaEstabaFinalizada)
            {
                return;
            }

            await Clients.Group(finalizacion.SalaId.ToString()).SendAsync(
                "salaFinalizada",
                new
                {
                    salaId = finalizacion.SalaId,
                    finalizadaUtc = finalizacion.FinalizadaUtc,
                    motivo = finalizacion.Motivo
                },
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

            var institucionId = ObtenerInstitucionId();

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
            var esConexionActiva = await _presencia.EsConexionActivaAsync(
                salaId,
                alumnoId,
                Context.ConnectionId,
                Context.ConnectionAborted);

            if (!esConexionActiva)
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
                    CancellationToken.None);
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

        private async Task<RegistroPresenciaDto> RegistrarPresenciaAlumnoAsync(
            Guid salaId,
            Guid alumnoId)
        {
            var registro = await _presencia.RegistrarConexionAsync(
                salaId,
                alumnoId,
                Context.ConnectionId,
                Context.ConnectionAborted);

            Context.Items["SalaId"] = salaId;
            Context.Items["AlumnoId"] = alumnoId;

            if (!string.IsNullOrWhiteSpace(registro.ConexionAnterior)
                && !string.Equals(registro.ConexionAnterior, Context.ConnectionId, StringComparison.Ordinal))
            {
                await Groups.RemoveFromGroupAsync(
                    registro.ConexionAnterior,
                    salaId.ToString(),
                    Context.ConnectionAborted);
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                salaId.ToString(),
                Context.ConnectionAborted);

            return registro;
        }

        private async Task NotificarIngresoAlumnoAsync(
            Guid salaId,
            Guid alumnoId,
            RegistroPresenciaDto registro)
        {
            // El conteo sale del registro atómico del script Lua. La lectura completa
            // del hash queda solo para componer el snapshot de alumnosConectados.
            var presencia = await _presencia.ObtenerPresenciaAsync(
                salaId,
                CancellationToken.None);

            await Clients.Group($"{salaId}:docentes").SendAsync(
                "alumnoIngreso",
                new
                {
                    salaId,
                    alumnoId,
                    alias = registro.Alumno.Alias,
                    totalConectados = registro.TotalConectados,
                    ingresoUtc = registro.Alumno.IngresoUtc
                },
                CancellationToken.None);

            await Clients.Group($"{salaId}:docentes").SendAsync(
                "alumnosConectados",
                presencia,
                CancellationToken.None);
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

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var esDocente = Context.Items.TryGetValue("Rol", out var rol)
                                && string.Equals(
                                    rol?.ToString(),
                                    RolDocente,
                                    StringComparison.OrdinalIgnoreCase);

                if (!esDocente
                    && Context.Items.TryGetValue("SalaId", out var salaIdObj)
                    && salaIdObj is Guid salaId
                    && salaId != Guid.Empty
                    && Context.Items.TryGetValue("AlumnoId", out var alumnoIdObj)
                    && alumnoIdObj is Guid alumnoId)
                {
                    var resultado = await _presencia.EliminarConexionAsync(
                        salaId,
                        alumnoId,
                        Context.ConnectionId,
                        CancellationToken.None);

                    if (resultado.AlumnoEliminado && resultado.Alumno is not null)
                    {
                        var presencia = await _presencia.ObtenerPresenciaAsync(
                            salaId,
                            CancellationToken.None);

                        await Clients.Group($"{salaId}:docentes").SendAsync(
                            "alumnoSalio",
                            new
                            {
                                salaId,
                                alumnoId = resultado.Alumno.AlumnoId,
                                alias = resultado.Alumno.Alias,
                                totalConectados = presencia.TotalConectados,
                                salidaUtc = DateTime.UtcNow
                            },
                            CancellationToken.None);

                        await Clients.Group($"{salaId}:docentes").SendAsync(
                            "alumnosConectados",
                            presencia,
                            CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo actualizar la presencia al desconectar la conexión {ConnectionId}.",
                    Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
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
