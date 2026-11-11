using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.Cursos.Application.DTOs.Materiales;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Materiales.Commands;
using PEI.Cursos.Application.UseCases.Materiales.Queries;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Api.Controllers;

/* Controlador REST Nivel 2 para la gestion de materiales y apuntes de estudio (Caso de Uso S2-CU-01 / Issue #137).
Expone las operaciones de subida multipart, listado, baja logica y obtencion de texto interno para IA. */
[Authorize]
[ApiController]
[Route("api/v1")]
public class MaterialesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaterialesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /* Sube un apunte PDF al curso, extrae el texto en memoria y persiste el documento desacoplado.
    Retorna 201 Created con cabecera Location /api/v1/cursos/{cursoId}/materiales/{id}. */
    [HttpPost("cursos/{cursoId:guid}/materiales")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MaterialCursoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> SubirMaterial(
        [FromRoute] Guid cursoId,
        [FromForm] IFormFile archivo,
        [FromForm] string? titulo,
        CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            throw new DomainException("El archivo es obligatorio.");
        }

        var command = new SubirMaterialCommand(
            cursoId,
            titulo,
            archivo.FileName,
            archivo.Length,
            archivo.ContentType,
            archivo.OpenReadStream()
        );

        var materialCreado = await _mediator.SendAsync<SubirMaterialCommand, MaterialCursoDto>(
            command,
            cancellationToken
        );

        var locationUri = $"/api/v1/cursos/{cursoId}/materiales/{materialCreado.Id}";
        return Created(locationUri, materialCreado);
    }

    /* Consulta el catalogo de materiales activos del curso ordenados del mas reciente al mas antiguo.
    Omite el texto completo del documento para optimizar la transferencia de red. */
    [HttpGet("cursos/{cursoId:guid}/materiales")]
    [ProducesResponseType(typeof(IReadOnlyList<MaterialCursoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarMateriales(
        [FromRoute] Guid cursoId,
        CancellationToken cancellationToken)
    {
        var query = new ListarMaterialesPorCursoQuery(cursoId);

        var materiales = await _mediator.SendAsync<ListarMaterialesPorCursoQuery, IReadOnlyList<MaterialCursoDto>>(
            query,
            cancellationToken
        );

        return Ok(materiales);
    }

    /* Ejecuta la baja logica del material preservando el archivo fisico para consistencia con evaluaciones historicas. */
    [HttpDelete("cursos/{cursoId:guid}/materiales/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarMaterial(
        [FromRoute] Guid cursoId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new EliminarMaterialCommand(cursoId, id);

        await _mediator.SendAsync<EliminarMaterialCommand, bool>(
            command,
            cancellationToken
        );

        return NoContent();
    }

    /* Endpoint interno consumido por el microservicio de IA (recurso MCP apunte://{id}) para la generacion de preguntas. */
    [HttpGet("materiales/{id:guid}/texto")]
    [ProducesResponseType(typeof(TextoMaterialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerTextoMaterial(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new ObtenerTextoMaterialQuery(id);

        var textoMaterial = await _mediator.SendAsync<ObtenerTextoMaterialQuery, TextoMaterialDto>(
            query,
            cancellationToken
        );

        return Ok(textoMaterial);
    }
}
