using Microsoft.AspNetCore.Mvc;
using PEI.IA.Application.DTOs;
using PEI.IA.Application.Interfaces;

namespace PEI.IA.Api.Controllers;

/// <summary>
/// Controlador para la sugerencia de preguntas evaluativas a partir de un apunte de curso.
/// Consumido por el Frontend Docente (S3-FE-08) vía API Gateway.
/// </summary>
[ApiController]
[Route("api/v1/sugerencias-preguntas")]
[Produces("application/json")]
public class SugerenciasController : ControllerBase
{
    private readonly IAgentesOrchestrator _agentesOrchestrator;
    private readonly ILogger<SugerenciasController> _logger;

    public SugerenciasController(
        IAgentesOrchestrator agentesOrchestrator,
        ILogger<SugerenciasController> logger)
    {
        _agentesOrchestrator = agentesOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Sugiere preguntas de evaluación a partir de un apunte del curso en estado Listo.
    /// </summary>
    /// <param name="request">Parámetros de solicitud (materialId, cantidad 1-10, tipos, tema).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Colección de preguntas sugeridas verificadas por el agente revisor.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(SugerirPreguntasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SugerirPreguntas(
        [FromBody] SugerirPreguntasRequest request,
        CancellationToken cancellationToken)
    {
        // ── Validaciones según OpenAPI ia-v1.json ──
        if (request.MaterialId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "El materialId es obligatorio.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Cantidad < 1 || request.Cantidad > 10)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "La cantidad tiene que estar entre 1 y 10.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Tipos != null)
        {
            var tiposValidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OpcionMultiple", "Abierta" };
            if (request.Tipos.Any(t => !tiposValidos.Contains(t)))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Los tipos de pregunta permitidos son 'OpcionMultiple' y 'Abierta'.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        if (request.Tema != null && request.Tema.Length > 200)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "El tema no puede exceder los 200 caracteres.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Validación de rol si se encuentra presente en los claims del token
        var rol = User?.Claims?.FirstOrDefault(c => c.Type == "rol" || c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!string.IsNullOrWhiteSpace(rol) &&
            !rol.Equals("Docente", StringComparison.OrdinalIgnoreCase) &&
            !rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Solo los docentes pueden pedir sugerencias.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        try
        {
            // Timeout de 60 s fijado en contrato OpenAPI
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var resultado = await _agentesOrchestrator.GenerarPreguntasAsync(request, cts.Token);
            return Ok(resultado);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout de 60s excedido al sugerir preguntas para apunte {MaterialId}.", request.MaterialId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Type = "IA_NO_DISPONIBLE",
                Title = "La IA no está disponible. Probá de nuevo en unos minutos.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MATERIAL_SIN_TEXTO"))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "MATERIAL_SIN_TEXTO",
                Title = "Este apunte no tiene texto para generar preguntas (puede ser un PDF escaneado). Elegí otro apunte.",
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Type = "MATERIAL_NO_ENCONTRADO",
                Title = "No encontramos el apunte. Puede que lo hayan eliminado: elegí otro.",
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al sugerir preguntas.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Type = "IA_NO_DISPONIBLE",
                Title = "La IA no está disponible. Probá de nuevo en unos minutos.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}
