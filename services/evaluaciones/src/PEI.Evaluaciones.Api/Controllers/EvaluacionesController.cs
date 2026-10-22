using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEI.Evaluaciones.Application.Evaluaciones;

namespace PEI.Evaluaciones.Api.Controllers;

public record EvaluacionRequest(string Titulo, string Tipo, JsonElement PreguntasJson);
public record PublicarEvaluacionRequest(int VersionEsperada, Guid CohorteId);

[ApiController]
[Route("api/v1/evaluaciones")]
public class EvaluacionesController : ControllerBase
{
    private const string EncabezadoInstitucion = "X-Institucion-Id";

    private readonly IMediator _mediador;

    public EvaluacionesController(IMediator mediador)
    {
        _mediador = mediador;
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        [FromBody] EvaluacionRequest peticion,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var resultado = await _mediador.Send(
            new CrearEvaluacionCommand(institucionId, peticion.Titulo, peticion.Tipo, peticion.PreguntasJson), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        var evaluacion = resultado.Valor!;
        return CreatedAtAction(nameof(ObtenerPorId), new { id = evaluacion.Id }, evaluacion);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var evaluaciones = await _mediador.Send(new ListarEvaluacionesQuery(institucionId), ct);
        return Ok(evaluaciones);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(
        Guid id,
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var resultado = await _mediador.Send(new ObtenerEvaluacionQuery(id, institucionId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        [FromBody] EvaluacionRequest peticion,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var resultado = await _mediador.Send(
            new ActualizarEvaluacionCommand(id, institucionId, peticion.Titulo, peticion.Tipo, peticion.PreguntasJson), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(
        Guid id,
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var resultado = await _mediador.Send(new EliminarEvaluacionCommand(id, institucionId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/publicar")]
    public async Task<IActionResult> Publicar(
        Guid id,
        [FromHeader(Name = EncabezadoInstitucion)] Guid institucionId,
        [FromBody] PublicarEvaluacionRequest peticion,
        CancellationToken ct)
    {
        if (institucionId == Guid.Empty)
        {
            return FaltaInstitucion();
        }

        var resultado = await _mediador.Send(
            new PublicarEvaluacionCommand(id, institucionId, peticion.VersionEsperada, peticion.CohorteId), ct);

        if (resultado.Tipo != TipoResultado.Ok)
        {
            return RespuestaError(resultado);
        }

        return Ok(resultado.Valor);
    }

    private IActionResult FaltaInstitucion()
    {
        return Problema(400, "Falta el encabezado X-Institucion-Id con un identificador valido.", Array.Empty<string>());
    }

    private IActionResult RespuestaError<T>(ResultadoOperacion<T> resultado)
    {
        return resultado.Tipo switch
        {
            TipoResultado.NoEncontrado => Problema(404, "La evaluacion no existe.", resultado.Errores),
            TipoResultado.Conflicto => Problema(409, "La operacion no se puede realizar en el estado actual.", resultado.Errores),
            _ => Problema(400, "La peticion no es valida.", resultado.Errores)
        };
    }

    private ObjectResult Problema(int estado, string titulo, IReadOnlyList<string> errores)
    {
        var problema = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = errores.Count > 0 ? string.Join(" | ", errores) : null
        };

        problema.Extensions["errores"] = errores;

        return new ObjectResult(problema) { StatusCode = estado };
    }
}
