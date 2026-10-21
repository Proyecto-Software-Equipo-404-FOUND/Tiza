using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.Cursos.Application.Common.DTOs;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Cursos.Commands;
using PEI.Cursos.Application.UseCases.Cursos.Queries;

namespace PEI.Cursos.Api.Controllers;

/* Controlador REST Nivel 2 para la gestion de materias y espacios curriculares (Curso).
Delega la ejecucion de comandos y consultas al mediador liviano garantizando el aislamiento multi-tenant. */
[ApiController]
[Route("api/v1/cursos")]
public class CursosController : ControllerBase
{
    private readonly IMediator _mediator;

    public CursosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /* Obtiene el detalle individual de un curso especifico.
    Respalda la resolucion de la cabecera Location en la creacion o emite 404 Not Found si no existe. */
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CursoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var curso = await _mediator.SendAsync<GetCursoByIdQuery, CursoDto?>(
            new GetCursoByIdQuery(id),
            cancellationToken
        );

        if (curso is null)
        {
            return NotFound(new { error = $"No se encontro el curso con ID '{id}' o no pertenece a la institucion." });
        }

        return Ok(curso);
    }

    /* Consulta el catalogo de cursos de la institucion con paginacion y filtros opcionales de area y busqueda. */
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginadoDTO<CursoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCursos(
        [FromQuery] string? area,
        [FromQuery] string? busqueda,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCursosQuery(area, busqueda, pagina, tamanoPagina);

        var resultado = await _mediator.SendAsync<GetCursosQuery, ResultadoPaginadoDTO<CursoDto>>(
            query,
            cancellationToken
        );

        return Ok(resultado);
    }

    /* Da de alta un nuevo curso dentro del tenant actual retornando 201 Created con cabecera Location. */
    [HttpPost]
    [ProducesResponseType(typeof(CursoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCurso(
        [FromBody] CrearCursoDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CrearCursoCommand(dto.Nombre, dto.Area);

        var cursoCreado = await _mediator.SendAsync<CrearCursoCommand, CursoDto>(
            command,
            cancellationToken
        );

        return CreatedAtAction(nameof(GetById), new { id = cursoCreado.Id }, cursoCreado);
    }

    /* Modifica los datos descriptivos de un curso existente garantizando aislamiento tenant. */
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CursoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCurso(
        [FromRoute] Guid id,
        [FromBody] ActualizarCursoDto dto,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarCursoCommand(id, dto.Nombre, dto.Area);

        var cursoActualizado = await _mediator.SendAsync<ActualizarCursoCommand, CursoDto?>(
            command,
            cancellationToken
        );

        if (cursoActualizado is null)
        {
            return NotFound(new { error = $"No se encontro el curso con ID '{id}' o no pertenece a la institucion." });
        }

        return Ok(cursoActualizado);
    }

    /* Ejecuta la baja logica de un curso impidiendo aperturas futuras. */
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesactivarCurso(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DesactivarCursoCommand(id);

        var desactivado = await _mediator.SendAsync<DesactivarCursoCommand, bool>(
            command,
            cancellationToken
        );

        if (!desactivado)
        {
            return NotFound(new { error = $"No se encontro el curso con ID '{id}' o no pertenece a la institucion." });
        }

        return NoContent();
    }
}
