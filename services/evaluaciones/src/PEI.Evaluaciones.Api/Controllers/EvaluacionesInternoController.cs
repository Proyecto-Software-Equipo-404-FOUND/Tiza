using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEI.Evaluaciones.Api.Seguridad;
using PEI.Evaluaciones.Application.Evaluaciones;

namespace PEI.Evaluaciones.Api.Controllers;

// S3-EV-02 (#223): Sala en vivo lee las preguntas de una evaluación por acá, con la clave interna.
// Antes usaba GET /api/v1/evaluaciones/{id} con X-Institucion-Id, que ahora exige el JWT del docente dueño.
// Endpoint interno entre servicios: no se publica en el gateway.
[ApiController]
[Route("interno/evaluaciones")]
public sealed class EvaluacionesInternoController : ControllerBase
{
    private const string EncabezadoInstitucion = "X-Institucion-Id";

    private readonly IMediator _mediador;
    private readonly IConfiguration _configuracion;

    public EvaluacionesInternoController(IMediator mediador, IConfiguration configuracion)
    {
        _mediador = mediador;
        _configuracion = configuracion;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(
        Guid id,
        [FromHeader(Name = ClaveInterna.Header)] string? apiKey,
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        CancellationToken ct)
    {
        if (!ClaveInterna.EsValida(_configuracion, apiKey))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credencial interna inválida.");
        }

        if (institucionId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Falta el encabezado X-Institucion-Id.");
        }

        var resultado = await _mediador.Send(new ObtenerEvaluacionQuery(id, institucionId), ct);

        return resultado.Tipo == TipoResultado.Ok
            ? Ok(resultado.Valor)
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "La evaluacion no existe.");
    }
}
