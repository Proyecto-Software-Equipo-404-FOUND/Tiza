using MediatR;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public record EliminarEvaluacionCommand(Guid Id, Guid InstitucionId)
    : IRequest<ResultadoOperacion<bool>>;

public class EliminarEvaluacionHandler : IRequestHandler<EliminarEvaluacionCommand, ResultadoOperacion<bool>>
{
    private readonly IEvaluacionRepositorio _repositorio;

    public EliminarEvaluacionHandler(IEvaluacionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<ResultadoOperacion<bool>> Handle(EliminarEvaluacionCommand request, CancellationToken ct)
    {
        var evaluacion = await _repositorio.ObtenerPorIdAsync(request.Id, request.InstitucionId, ct);

        if (evaluacion is null)
        {
            return ResultadoOperacion<bool>.NoEncontrado();
        }

        if (evaluacion.Estado != EstadoEvaluacion.Borrador)
        {
            return ResultadoOperacion<bool>.Conflicto("Solo se pueden borrar evaluaciones en estado Borrador.");
        }

        _repositorio.Eliminar(evaluacion);
        await _repositorio.GuardarCambiosAsync(ct);

        return ResultadoOperacion<bool>.Ok(true);
    }
}