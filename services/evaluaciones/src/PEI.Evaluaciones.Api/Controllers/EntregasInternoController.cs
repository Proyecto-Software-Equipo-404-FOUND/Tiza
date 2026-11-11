using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEI.Evaluaciones.Api.Seguridad;
using PEI.Evaluaciones.Application.Correccion;
using PEI.Evaluaciones.Application.Evaluaciones;

namespace PEI.Evaluaciones.Api.Controllers;

public sealed record CalificacionSugeridaRequest(
    Guid PreguntaId,
    decimal NotaSugerida,
    string? Justificacion,
    string? DesgloseJson);

// S2-EV-02 (#144). Consumidor: IA, tool MCP RegistrarCalificacionSugerida (#146).
// Contrato: POST /interno/entregas/{entregaId}/calificacion-sugerida (evaluaciones-v1.json).
[ApiController]
[Route("interno/entregas")]
public sealed class EntregasInternoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuracion;

    public EntregasInternoController(IMediator mediator, IConfiguration configuracion)
    {
        _mediator = mediator;
        _configuracion = configuracion;
    }

    [HttpPost("{entregaId:guid}/calificacion-sugerida")]
    [ProducesResponseType(typeof(CalificacionSugeridaRegistrada), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarCalificacionSugerida(
        Guid entregaId,
        [FromHeader(Name = ClaveInterna.Header)] string? apiKey,
        [FromBody] CalificacionSugeridaRequest peticion,
        CancellationToken ct)
    {
        if (!ClaveInterna.EsValida(_configuracion, apiKey))
            return Problema(StatusCodes.Status401Unauthorized, "Credencial interna inválida.");

        var resultado = await _mediator.Send(new RegistrarCalificacionSugeridaCommand(
            entregaId, peticion.PreguntaId, peticion.NotaSugerida, peticion.Justificacion, peticion.DesgloseJson), ct);

        return resultado.Tipo switch
        {
            TipoResultado.Ok => Ok(resultado.Valor),
            TipoResultado.NoEncontrado => Problema(StatusCodes.Status404NotFound, "No existe la entrega o la pregunta indicada."),
            TipoResultado.Conflicto => Problema(StatusCodes.Status409Conflict, resultado.Errores.FirstOrDefault() ?? "Conflicto.", resultado.Errores),
            _ => Problema(StatusCodes.Status400BadRequest, resultado.Errores.FirstOrDefault() ?? "Datos inválidos.", resultado.Errores)
        };
    }

    private static ObjectResult Problema(int status, string titulo, IReadOnlyList<string>? errores = null)
    {
        var problema = new ProblemDetails { Status = status, Title = titulo };
        problema.Extensions["errores"] = errores ?? Array.Empty<string>();
        return new ObjectResult(problema) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
