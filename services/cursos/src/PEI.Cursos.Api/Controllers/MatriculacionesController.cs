using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.Cursos.Api.Security;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

namespace PEI.Cursos.Api.Controllers;

/* Controlador REST Nivel 2 dedicado a la matriculación e inscripciones formales (Casos de Uso CU-04 y S2-CU-01).
Aplica el principio de Responsabilidad Única (SRP) bajo la sub-ruta anidada api/v1/cohortes/{cohorteId}/matriculas. */
[Authorize]
[ApiController]
[Route("api/v1/cohortes/{cohorteId:guid}/matriculas")]
public class MatriculacionesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatriculacionesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /* Obtiene el listado de matriculaciones asociadas a la cohorte especificada.
    Soporta filtrado opcional por matriculaciones activas para consumo de Sala en Vivo (Caso de Uso S2-CU-01).
    Retorna 404 Not Found si la cohorte no existe o pertenece a otra institucion. */
    [Authorize(Policy = CursosAuthorization.AdministradorODocente)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MatriculacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatriculaciones(
        [FromRoute] Guid cohorteId,
        [FromQuery] bool? soloActivas,
        CancellationToken cancellationToken)
    {
        var query = new GetMatriculacionesByCohorteQuery(cohorteId, soloActivas);

        var matriculaciones = await _mediator.SendAsync<GetMatriculacionesByCohorteQuery, IReadOnlyList<MatriculacionDto>?>(
            query,
            cancellationToken
        );

        if (matriculaciones is null)
        {
            return NotFound(new { error = $"No se encontro la cohorte con ID '{cohorteId}'." });
        }

        return Ok(matriculaciones);
    }

    /* Obtiene el detalle individual de una inscripción en una cohorte.
    Respalda la resolución de la cabecera Location generada tras la matriculación. */
    [Authorize(Policy = CursosAuthorization.AdministradorODocente)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MatriculacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid cohorteId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var matriculacion = await _mediator.SendAsync<GetMatriculacionByIdQuery, MatriculacionDto?>(
            new GetMatriculacionByIdQuery(id),
            cancellationToken
        );

        if (matriculacion is null || matriculacion.CohorteId != cohorteId)
        {
            return NotFound(new { error = $"No se encontro la matriculacion con ID '{id}' en la cohorte especificada." });
        }

        return Ok(matriculacion);
    }

    /* Registra la matriculación formal de un alumno en la cohorte especificada.
    Valida pertenencia institucional, estado activo de la comisión y unicidad preventiva. */
    [Authorize(Policy = CursosAuthorization.AdministradorODocente)]
    [HttpPost]
    [ProducesResponseType(typeof(MatriculacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MatricularAlumno(
        [FromRoute] Guid cohorteId,
        [FromBody] MatricularAlumnoDto dto,
        CancellationToken cancellationToken)
    {
        var command = new MatricularAlumnoCommand(cohorteId, dto.AlumnoId);

        var matriculacionCreada = await _mediator.SendAsync<MatricularAlumnoCommand, MatriculacionDto>(
            command,
            cancellationToken
        );

        return CreatedAtAction(
            nameof(GetById),
            new { cohorteId, id = matriculacionCreada.Id },
            matriculacionCreada
        );
    }

    /* Da de baja la matricula de un alumno en la cohorte especificada.
    Retorna 204 NoContent en exito, 404 NotFound si no existe o no corresponde a la cohorte,
    o 409 Conflict via ProblemDetailsMiddleware si el estado actual no admite la baja. */
    [Authorize(Policy = CursosAuthorization.AdministradorODocente)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DarDeBaja(
        [FromRoute] Guid cohorteId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DarDeBajaMatriculacionCommand(cohorteId, id);

        var resultado = await _mediator.SendAsync<DarDeBajaMatriculacionCommand, bool>(
            command,
            cancellationToken
        );

        if (!resultado)
        {
            return NotFound(new { error = $"No se encontro la matriculacion con ID '{id}' en la cohorte especificada." });
        }

        return NoContent();
    }

    /* Reactiva una matricula previamente dada de baja reincorporando al alumno en la cohorte.
    Retorna 204 NoContent en exito, 404 NotFound si no existe o no corresponde a la cohorte,
    o 409 Conflict via ProblemDetailsMiddleware si el estado actual no admite la reactivacion. */
    [Authorize(Policy = CursosAuthorization.AdministradorODocente)]
    [HttpPost("{id:guid}/reactivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivar(
        [FromRoute] Guid cohorteId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ReactivarMatriculacionCommand(cohorteId, id);

        var resultado = await _mediator.SendAsync<ReactivarMatriculacionCommand, bool>(
            command,
            cancellationToken
        );

        if (!resultado)
        {
            return NotFound(new { error = $"No se encontro la matriculacion con ID '{id}' en la cohorte especificada." });
        }

        return NoContent();
    }

    /* Indica si un alumno tiene matrícula activa en la cohorte (200 con la matrícula o 404).
    Lo consume Sala en Vivo cuando un alumno se une por PIN, reenviando el token del propio alumno:
    por eso un alumno puede consultarse solo a sí mismo; administrador y docente, a cualquiera.
    Una cohorte inactiva no habilita el ingreso aunque conserve matrículas activas. */
    [Authorize(Policy = CursosAuthorization.GestionOAlumnoPropio)]
    [HttpGet("alumnos/{alumnoId:guid}")]
    [ProducesResponseType(typeof(MatriculacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatriculaActivaDeAlumno(
        [FromRoute] Guid cohorteId,
        [FromRoute] Guid alumnoId,
        CancellationToken cancellationToken)
    {
        var esAdministradorODocente = User.IsInRole(CursosAuthorization.Administrador)
            || User.IsInRole(CursosAuthorization.Docente);
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!esAdministradorODocente && !string.Equals(sub, alumnoId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var matriculaciones = await _mediator.SendAsync<GetMatriculacionesByCohorteQuery, IReadOnlyList<MatriculacionDto>?>(
            new GetMatriculacionesByCohorteQuery(cohorteId, true, SoloCohorteActiva: true),
            cancellationToken
        );

        var matricula = matriculaciones?.FirstOrDefault(m => m.AlumnoId == alumnoId);

        if (matricula is null)
        {
            return NotFound(new { error = "El alumno no tiene una matrícula activa en la cohorte indicada." });
        }

        return Ok(matricula);
    }
}
