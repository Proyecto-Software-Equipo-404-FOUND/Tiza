using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Application.Validacion;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record ActualizarEvaluacionCommand(
    Guid Id,
    Guid InstitucionId,
    string Titulo,
    string Tipo,
    JsonElement PreguntasJson) : IRequest<ResultadoOperacion<EvaluacionDto>>;

public class ActualizarEvaluacionHandler : IRequestHandler<ActualizarEvaluacionCommand, ResultadoOperacion<EvaluacionDto>>
{
    private readonly IEvaluacionRepositorio _repositorio;
    private readonly ValidadorPreguntas _validador;

    public ActualizarEvaluacionHandler(IEvaluacionRepositorio repositorio, ValidadorPreguntas validador)
    {
        _repositorio = repositorio;
        _validador = validador;
    }

    public async Task<ResultadoOperacion<EvaluacionDto>> Handle(ActualizarEvaluacionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) || request.Titulo.Length > 200)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("El titulo es obligatorio y no puede superar 200 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Tipo) || request.Tipo.Length > 50)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("El tipo es obligatorio y no puede superar 50 caracteres.");
        }

        if (request.PreguntasJson.ValueKind == JsonValueKind.Undefined)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("Las preguntas son obligatorias.");
        }

        var json = request.PreguntasJson.GetRawText();
        var validacion = _validador.Validar(json);

        if (!validacion.EsValido)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido(validacion.Errores);
        }

        var evaluacion = await _repositorio.ObtenerPorIdAsync(request.Id, request.InstitucionId, ct);

        if (evaluacion is null)
        {
            return ResultadoOperacion<EvaluacionDto>.NoEncontrado();
        }

        if (evaluacion.Estado != EstadoEvaluacion.Borrador)
        {
            return ResultadoOperacion<EvaluacionDto>.Conflicto("Solo se pueden modificar evaluaciones en estado Borrador.");
        }

        evaluacion.Titulo = request.Titulo.Trim();
        evaluacion.Tipo = request.Tipo.Trim();
        evaluacion.PreguntasJson = json;
        evaluacion.FechaActualizacion = DateTimeOffset.UtcNow;

        await _repositorio.GuardarCambiosAsync(ct);

        return ResultadoOperacion<EvaluacionDto>.Ok(EvaluacionDto.Desde(evaluacion));
    }
}