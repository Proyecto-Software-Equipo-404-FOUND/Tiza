using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Cohortes.Commands;
using PEI.Cursos.Application.UseCases.Cohortes.Queries;

namespace PEI.Cursos.Api.Controllers;

/* Controlador REST Nivel 2 dedicado a la administracion de comisiones y divisiones (Cohorte).
Aplica el principio de Responsabilidad Unica (SRP) bajo la sub-ruta anidada api/v1/cursos/{cursoId}/cohortes. */
[ApiController]
[Route("api/v1/cursos/{cursoId:guid}/cohortes")]
public class CohortesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CohortesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /* Obtiene el listado de todas las cohortes asociadas al curso especificado.
    Retorna 404 Not Found si el curso padre no existe para la institucion autenticada. */
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CohorteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCohortes(
        [FromRoute] Guid cursoId,
        CancellationToken cancellationToken)
    {
        var cohortes = await _mediator.SendAsync<GetCohortesByCursoQuery, IReadOnlyList<CohorteDto>?>(
            new GetCohortesByCursoQuery(cursoId),
            cancellationToken
        );

        if (cohortes is null)
        {
            return NotFound(new { error = $"No se encontro el curso con ID '{cursoId}' o no pertenece a la institucion." });
        }

        return Ok(cohortes);
    }

    /* Registra una nueva cohorte para el curso especificado validando docente, periodo y unicidad. */
    [HttpPost]
    [ProducesResponseType(typeof(CohorteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCohorte(
        [FromRoute] Guid cursoId,
        [FromBody] CrearCohorteDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CrearCohorteCommand(cursoId, dto.Nombre, dto.Periodo, dto.DocenteId);

        var cohorteCreada = await _mediator.SendAsync<CrearCohorteCommand, CohorteDto>(
            command,
            cancellationToken
        );

        return CreatedAtAction(nameof(GetCohortes), new { cursoId }, cohorteCreada);
    }

    /* Valida la existencia y pertenencia institucional de una cohorte (Caso de Uso CU-05).
    Endpoint inter-servicio consumido por Evaluaciones para vincular evaluaciones a comisiones.
    Retorna 200 OK con metadatos si pertenece a la institucion, o 404 Not Found si no existe. */
    [HttpGet("/api/v1/cohortes/{id:guid}/validacion")]
    [ProducesResponseType(typeof(CohorteValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidarCohorte(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await _mediator.SendAsync<ValidarCohorteQuery, CohorteValidacionDto?>(
            new ValidarCohorteQuery(id),
            cancellationToken
        );

        if (resultado is null)
        {
            return NotFound(new { error = $"No se encontro la cohorte con ID '{id}' o no pertenece a la institucion." });
        }

        return Ok(resultado);
    }
}
