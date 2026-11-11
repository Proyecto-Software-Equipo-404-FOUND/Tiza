using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEI.Evaluaciones.Application.Evaluaciones;

namespace PEI.Evaluaciones.Api.Controllers;

[ApiController]
[Route("interno/salas")]
public sealed class SalasInternoController : ControllerBase
{
    private const string ApiKeyHeader = "X-Api-Key-Interna";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public SalasInternoController(
        IMediator mediator,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpPost("{salaId:guid}/entregas")]
    [ProducesResponseType(typeof(LoteEntregasResultado), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecibirLote(
        Guid salaId,
        [FromHeader(Name = ApiKeyHeader)] string? apiKey,
        [FromBody] LoteEntregasRequest lote,
        CancellationToken ct)
    {
        var claveConfigurada = _configuration["ServiciosInternos:ApiKey"];

        if (!CoincideApiKey(claveConfigurada, apiKey))
        {
            return CrearProblema(
                StatusCodes.Status401Unauthorized,
                "Credencial interna inválida.");
        }

        var resultado = await _mediator.Send(
            new RecibirLoteEntregasCommand(salaId, lote),
            ct);

        return resultado.Tipo switch
        {
            TipoResultado.Ok => Ok(resultado.Valor),
            TipoResultado.NoEncontrado => CrearProblema(
                StatusCodes.Status404NotFound,
                "No existe una evaluación publicada con ese id."),
            TipoResultado.Conflicto => CrearProblema(
                StatusCodes.Status409Conflict,
                "La operación no se puede realizar en el estado actual.",
                resultado.Errores),
            _ => CrearProblema(
                StatusCodes.Status400BadRequest,
                "El lote no es válido.",
                resultado.Errores)
        };
    }

    private static bool CoincideApiKey(string? esperada, string? recibida)
    {
        if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(recibida))
        {
            return false;
        }

        var bytesEsperados = Encoding.UTF8.GetBytes(esperada);
        var bytesRecibidos = Encoding.UTF8.GetBytes(recibida);

        return bytesEsperados.Length == bytesRecibidos.Length &&
               CryptographicOperations.FixedTimeEquals(
                   bytesEsperados,
                   bytesRecibidos);
    }

    private ObjectResult CrearProblema(
        int status,
        string titulo,
        IReadOnlyList<string>? errores = null)
    {
        errores ??= Array.Empty<string>();

        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = errores.Count > 0
                ? string.Join(" | ", errores)
                : null
        };

        problema.Extensions["errores"] = errores;

        return new ObjectResult(problema)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}