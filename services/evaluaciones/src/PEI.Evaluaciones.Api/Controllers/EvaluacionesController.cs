using PEI.Evaluaciones.Application.Evaluaciones.Queries.GetMisExamenes;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.Evaluaciones.Application.Correccion;
using PEI.Evaluaciones.Application.Evaluaciones;


namespace PEI.Evaluaciones.Api.Controllers;

public record EvaluacionRequest(string Titulo, string Tipo, JsonElement PreguntasJson);
public record PublicarEvaluacionRequest(int VersionEsperada, Guid CohorteId);
public record EditarNotaRespuestaRequest(decimal Puntaje, string? Comentario, List<DesgloseItemDto>? DesglosePorCriterio);

[ApiController]
[Route("api/v1/evaluaciones")]
[Authorize]
// S3-EV-02 (#223, cierra #134): la institución, el usuario y el rol salen SIEMPRE del JWT.
// Ya no se lee X-Institucion-Id (si el front lo sigue mandando, se ignora).
// Sala en vivo lee las preguntas por /interno/evaluaciones/{id} (EvaluacionesInternoController).
public class EvaluacionesController : ControllerBase
{
    private readonly IMediator _mediador;

    public EvaluacionesController(IMediator mediador)
    {
        _mediador = mediador;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] EvaluacionRequest peticion, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeCrear(sesion.Rol))
        {
            return Problema(403, "Solo los docentes pueden crear exámenes.", Array.Empty<string>());
        }

        var resultado = await _mediador.Send(
            new CrearEvaluacionCommand(sesion.InstitucionId, peticion.Titulo, peticion.Tipo, peticion.PreguntasJson, sesion.UsuarioId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        var evaluacion = resultado.Valor!;
        return CreatedAtAction(nameof(ObtenerPorId), new { id = evaluacion.Id }, evaluacion);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeLeer(sesion.Rol))
        {
            return Problema(403, "Solo docentes y administradores pueden ver los exámenes.", Array.Empty<string>());
        }

        var evaluaciones = await _mediador.Send(
            new ListarEvaluacionesQuery(sesion.InstitucionId, AccesoEvaluaciones.FiltroDocente(sesion.Rol, sesion.UsuarioId)), ct);
        return Ok(evaluaciones);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeLeer(sesion.Rol))
        {
            return Problema(403, "Solo docentes y administradores pueden ver los exámenes.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new ObtenerEvaluacionQuery(id, sesion.InstitucionId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] EvaluacionRequest peticion, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeModificar(sesion.Rol))
        {
            return Problema(403, "Solo el docente que creó el examen puede editarlo.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(
            new ActualizarEvaluacionCommand(id, sesion.InstitucionId, peticion.Titulo, peticion.Tipo, peticion.PreguntasJson), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeModificar(sesion.Rol))
        {
            return Problema(403, "Solo el docente que creó el examen puede eliminarlo.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new EliminarEvaluacionCommand(id, sesion.InstitucionId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/publicar")]
    public async Task<IActionResult> Publicar(Guid id, [FromBody] PublicarEvaluacionRequest peticion, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeModificar(sesion.Rol))
        {
            return Problema(403, "Solo el docente que creó el examen puede publicarlo.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(
            new PublicarEvaluacionCommand(id, sesion.InstitucionId, peticion.VersionEsperada, peticion.CohorteId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpGet("{id:guid}/entregas")]
    public async Task<IActionResult> ListarEntregas(Guid id, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeLeer(sesion.Rol))
        {
            return Problema(403, "Solo docentes y administradores pueden ver los resultados.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new ListarEntregasEvaluacionQuery(id, sesion.InstitucionId), ct);
        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpGet("{id:guid}/entregas/{entregaId:guid}")]
    public async Task<IActionResult> ObtenerEntrega(Guid id, Guid entregaId, CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeLeer(sesion.Rol))
        {
            return Problema(403, "Solo docentes y administradores pueden ver los resultados.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new ObtenerEntregaDetalleQuery(id, entregaId, sesion.InstitucionId), ct);
        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpPut("{id:guid}/entregas/{entregaId:guid}/respuestas/{preguntaId:guid}/nota")]
    public async Task<IActionResult> EditarNotaRespuesta(
        Guid id,
        Guid entregaId,
        Guid preguntaId,
        [FromBody] EditarNotaRespuestaRequest peticion,
        CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeModificar(sesion.Rol))
        {
            return Problema(403, "Solo el docente que creó el examen puede editar la nota.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new EditarNotaRespuestaCommand(
            id,
            entregaId,
            preguntaId,
            sesion.InstitucionId,
            sesion.UsuarioId,
            sesion.Rol,
            peticion.Puntaje,
            peticion.Comentario,
            peticion.DesglosePorCriterio), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpPost("{id:guid}/entregas/{entregaId:guid}/aprobacion")]
    public async Task<IActionResult> AprobarNota(
        Guid id,
        Guid entregaId,
        CancellationToken ct)
    {
        if (!LeerSesion(out var sesion, out var error)) return error!;
        if (!AccesoEvaluaciones.PuedeModificar(sesion.Rol))
        {
            return Problema(403, "Solo el docente que creó el examen puede aprobar la nota.", Array.Empty<string>());
        }

        var acceso = await ComprobarAcceso(id, sesion, ct);
        if (acceso is not null) return acceso;

        var resultado = await _mediador.Send(new AprobarNotaEntregaCommand(
            id,
            entregaId,
            sesion.InstitucionId,
            sesion.UsuarioId,
            sesion.Rol), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpGet("/api/v1/alumnos/me/evaluaciones")]
    public async Task<IActionResult> ListarMisEvaluaciones(CancellationToken ct)
    {
        var usuarioClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(usuarioClaim, out var alumnoId))
        {
            return Problema(401, "No autenticado.", Array.Empty<string>());
        }

        var rol = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
        if (!string.Equals(rol, "Alumno", StringComparison.OrdinalIgnoreCase))
        {
            return Problema(403, "Solo los alumnos tienen \"Mis exámenes\".", Array.Empty<string>());
        }

        var resultado = await _mediador.Send(new GetMisExamenesQuery(alumnoId, rol), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return Problema(400, resultado.Errores.FirstOrDefault() ?? "La peticion no es valida.", resultado.Errores);
        }

        return Ok(resultado.Valor);
    }

    private sealed record Sesion(Guid InstitucionId, Guid UsuarioId, string? Rol);

    // Claims del contrato JwtInstitucional (identidad-v1.json): sub, institucion_id, role.
    private bool LeerSesion(out Sesion sesion, out IActionResult? error)
    {
        var institucionClaim = User.FindFirst("institucion_id")?.Value;
        var usuarioClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var rol = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

        if (!Guid.TryParse(institucionClaim, out var institucionId) || !Guid.TryParse(usuarioClaim, out var usuarioId))
        {
            sesion = new Sesion(Guid.Empty, Guid.Empty, null);
            error = Problema(401, "No autenticado.", Array.Empty<string>());
            return false;
        }

        sesion = new Sesion(institucionId, usuarioId, rol);
        error = null;
        return true;
    }

    // Examen inexistente, de otra institución o de otro docente → 404 (no se revela que existe).
    private async Task<IActionResult?> ComprobarAcceso(Guid id, Sesion sesion, CancellationToken ct)
    {
        var acceso = await _mediador.Send(
            new ComprobarAccesoEvaluacionQuery(id, sesion.InstitucionId, AccesoEvaluaciones.FiltroDocente(sesion.Rol, sesion.UsuarioId)), ct);

        return acceso.Tipo == TipoResultado.Ok
            ? null
            : Problema(404, "La evaluacion no existe.", Array.Empty<string>());
    }

    private IActionResult RespuestaError<T>(ResultadoOperacion<T> resultado)
    {
        var error = resultado.Errores.FirstOrDefault();
        string? type = null;
        if (error != null)
        {
            if (error.Contains("aprobada", StringComparison.OrdinalIgnoreCase))
                type = "NOTA_YA_APROBADA";
            else if (error.Contains("abiertas sin corregir", StringComparison.OrdinalIgnoreCase))
                type = "FALTAN_CORRECCIONES";
            else if (error.Contains("entre 0 y", StringComparison.OrdinalIgnoreCase))
                type = "PUNTAJE_FUERA_DE_RANGO";
        }

        return resultado.Tipo switch
        {
            TipoResultado.NoEncontrado => Problema(404, error ?? "La evaluacion no existe.", resultado.Errores),
            TipoResultado.Conflicto => Problema(409, error ?? "La operacion no se puede realizar en el estado actual.", resultado.Errores, type),
            _ => error != null && error.Contains("Solo el docente", StringComparison.OrdinalIgnoreCase)
                ? Problema(403, error, resultado.Errores)
                : Problema(400, error ?? "La peticion no es valida.", resultado.Errores, type)
        };
    }

    private ObjectResult Problema(int estado, string titulo, IReadOnlyList<string> errores, string? type = null)
    {
        var problema = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Type = type,
            Detail = errores.Count > 0 ? string.Join(" | ", errores) : null
        };

        problema.Extensions["errores"] = errores;

        return new ObjectResult(problema) { StatusCode = estado, ContentTypes = { "application/problem+json" } };
    }
}
