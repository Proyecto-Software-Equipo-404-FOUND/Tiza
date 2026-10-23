using System.Text.Json;
using MediatR;
using PEI.Evaluaciones.Application.Validacion;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record PublicarEvaluacionCommand(
    Guid Id,
    Guid InstitucionId,
    int VersionEsperada,
    Guid CohorteId) : IRequest<ResultadoOperacion<EvaluacionDto>>;

public class PublicarEvaluacionHandler : IRequestHandler<PublicarEvaluacionCommand, ResultadoOperacion<EvaluacionDto>>
{
    private readonly IEvaluacionRepositorio _repositorio;
    private readonly ICursosClient _cursos;

    public PublicarEvaluacionHandler(IEvaluacionRepositorio repositorio, ICursosClient cursos)
    {
        _repositorio = repositorio;
        _cursos = cursos;
    }

    public async Task<ResultadoOperacion<EvaluacionDto>> Handle(PublicarEvaluacionCommand request, CancellationToken ct)
    {
        if (request.CohorteId == Guid.Empty)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("La cohorte es obligatoria.");
        }

        var evaluacion = await _repositorio.ObtenerPorIdAsync(request.Id, request.InstitucionId, ct);

        if (evaluacion is null)
        {
            return ResultadoOperacion<EvaluacionDto>.NoEncontrado();
        }

        if (request.VersionEsperada != evaluacion.Version)
        {
            return ResultadoOperacion<EvaluacionDto>.Conflicto("La evaluacion fue modificada por otra persona. Recargala y reintenta.");
        }

        if (evaluacion.Estado != EstadoEvaluacion.Borrador)
        {
            return ResultadoOperacion<EvaluacionDto>.Conflicto("Solo se pueden publicar evaluaciones en estado Borrador.");
        }

        if (ContarPreguntas(evaluacion.PreguntasJson) == 0)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("No se puede publicar una evaluacion sin preguntas.");
        }

        var cohorteValida = await _cursos.ExisteCohorteAsync(request.CohorteId, request.InstitucionId, ct);

        if (!cohorteValida)
        {
            return ResultadoOperacion<EvaluacionDto>.Invalido("La cohorte no existe o no pertenece a la institucion.");
        }

        evaluacion.Estado = EstadoEvaluacion.Publicada;
        evaluacion.CohorteId = request.CohorteId;
        evaluacion.Version++;
        evaluacion.FechaActualizacion = DateTimeOffset.UtcNow;

        await _repositorio.GuardarCambiosAsync(ct);

        return ResultadoOperacion<EvaluacionDto>.Ok(EvaluacionDto.Desde(evaluacion));
    }

    private static int ContarPreguntas(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            if (raiz.ValueKind == JsonValueKind.Array)
            {
                return raiz.GetArrayLength();
            }

            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("preguntas", out var preguntas)
                && preguntas.ValueKind == JsonValueKind.Array)
            {
                return preguntas.GetArrayLength();
            }

            return 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}