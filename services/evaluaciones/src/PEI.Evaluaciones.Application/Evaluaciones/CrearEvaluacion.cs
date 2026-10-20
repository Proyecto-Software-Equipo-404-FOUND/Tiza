using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Application.Validacion;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record CrearEvaluacionCommand(
    Guid InstitucionId,
    string Titulo,
    string Tipo,
    JsonElement PreguntasJson) : IRequest<ResultadoOperacion<EvaluacionDto>>;

public class CrearEvaluacionHandler : IRequestHandler<CrearEvaluacionCommand, ResultadoOperacion<EvaluacionDto>>
{
    private readonly IEvaluacionRepositorio _repositorio;
    private readonly ValidadorPreguntas _validador;

    public CrearEvaluacionHandler(IEvaluacionRepositorio repositorio, ValidadorPreguntas validador)
    {
        _repositorio = repositorio;
        _validador = validador;
    }

    public async Task<ResultadoOperacion<EvaluacionDto>> Handle(CrearEvaluacionCommand request, CancellationToken ct)
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

        var ahora = DateTimeOffset.UtcNow;

        var evaluacion = new Evaluacion
        {
            Id = Guid.NewGuid(),
            InstitucionId = request.InstitucionId,
            Titulo = request.Titulo.Trim(),
            Tipo = request.Tipo.Trim(),
            Estado = EstadoEvaluacion.Borrador,
            PreguntasJson = json,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
            Version = 1
        };

        await _repositorio.AgregarAsync(evaluacion, ct);
        await _repositorio.GuardarCambiosAsync(ct);

        return ResultadoOperacion<EvaluacionDto>.Ok(EvaluacionDto.Desde(evaluacion));
    }
}