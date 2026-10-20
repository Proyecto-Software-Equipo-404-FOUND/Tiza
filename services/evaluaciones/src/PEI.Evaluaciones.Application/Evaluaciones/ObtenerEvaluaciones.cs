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

public record ListarEvaluacionesQuery(Guid InstitucionId)
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

        return evaluaciones.Select(EvaluacionDto.Desde).ToList();
    }
}