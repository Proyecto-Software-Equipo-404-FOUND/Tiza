using Microsoft.AspNetCore.Mvc;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Api.Controllers;

/// <summary>
/// Controlador interno del servicio de IA para corrección automatizada de respuestas abiertas.
/// Consumido por el microservicio de Evaluaciones (no expuesto directamente a clientes).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CorreccionesController : ControllerBase
{
    private readonly ICorreccionService _correccionService;
    private readonly ILogger<CorreccionesController> _logger;

    public CorreccionesController(
        ICorreccionService correccionService,
        ILogger<CorreccionesController> logger)
    {
        _correccionService = correccionService;
        _logger = logger;
    }

    /// <summary>
    /// Solicita la corrección automatizada de una respuesta abierta basada en la rúbrica del docente.
    /// </summary>
    /// <param name="request">Datos de la respuesta a corregir con rúbrica y puntaje máximo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado con nota sugerida, justificación pedagógica y métricas de procesamiento.</returns>
    /// <response code="200">Corrección completada exitosamente.</response>
    /// <response code="400">Datos de entrada inválidos (ProblemDetails RFC 9457).</response>
    /// <response code="500">Error interno del servicio de IA.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CorregirRespuestaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CorregirRespuestaResponse>> Corregir(
        [FromBody] CorregirRespuestaRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recibida solicitud de corrección para pregunta: {Pregunta}",
            request.PreguntaEnunciado[..Math.Min(50, request.PreguntaEnunciado.Length)]);

        var resultado = await _correccionService.CorregirAsync(request, cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Health check del servicio de IA.
    /// </summary>
    /// <returns>Estado del servicio.</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "PEI.IA",
            timestamp = DateTime.UtcNow
        });
    }
}
