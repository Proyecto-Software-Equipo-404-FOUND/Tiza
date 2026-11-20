using MediatR;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record ObtenerEvaluacionQuery(Guid Id, Guid InstitucionId)
    : IRequest<ResultadoOperacion<EvaluacionDto>>;

public class ObtenerEvaluacionHandler : IRequestHandler<ObtenerEvaluacionQuery, ResultadoOperacion<EvaluacionDto>>
{
    private readonly IEvaluacionRepositorio _repositorio;

    public ObtenerEvaluacionHandler(IEvaluacionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<EvaluacionDto>> Handle(ObtenerEvaluacionQuery request, CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerPorIdAsync(request.Id, request.InstitucionId, ct);

        if (evaluacion is null)
        {
            return ResultadoOperacion<EvaluacionDto>.NoEncontrado();
        }

        return ResultadoOperacion<EvaluacionDto>.Ok(EvaluacionDto.Desde(evaluacion));
    }
}

// S3-EV-02 (#223): DocenteId null = sin filtro (Administrador). Con valor, solo los exámenes
// de ese docente y los anteriores al Sprint 3 (sin dueño).
public record ListarEvaluacionesQuery(Guid InstitucionId, Guid? DocenteId = null)
    : IRequest<IReadOnlyList<EvaluacionDto>>;

public class ListarEvaluacionesHandler : IRequestHandler<ListarEvaluacionesQuery, IReadOnlyList<EvaluacionDto>>
{
    private readonly IEvaluacionRepositorio _repositorio;

    public ListarEvaluacionesHandler(IEvaluacionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<EvaluacionDto>> Handle(ListarEvaluacionesQuery request, CancellationToken ct)
    {
        var evaluaciones = await _repositorio.ListarAsync(request.InstitucionId, ct);

        return evaluaciones
            .Where(e => AccesoEvaluaciones.PuedeVer(e, request.DocenteId))
            .Select(EvaluacionDto.Desde)
            .ToList();
    }
}