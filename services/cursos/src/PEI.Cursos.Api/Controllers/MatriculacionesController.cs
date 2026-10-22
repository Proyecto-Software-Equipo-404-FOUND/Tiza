using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

namespace PEI.Cursos.Api.Controllers;

/* Controlador REST Nivel 2 dedicado a la matriculación e inscripciones formales (Caso de Uso CU-04).
Aplica el principio de Responsabilidad Única (SRP) bajo la sub-ruta anidada api/v1/cohortes/{cohorteId}/matriculas. */
[ApiController]
[Route("api/v1/cohortes/{cohorteId:guid}/matriculas")]
public class MatriculacionesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatriculacionesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /* Obtiene el detalle individual de una inscripción en una cohorte.
    Respalda la resolución de la cabecera Location generada tras la matriculación. */
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
}
