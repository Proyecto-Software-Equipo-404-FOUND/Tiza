using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cohortes.Queries;

/* Manejador de la consulta GetCohortesByCursoQuery.
Verifica que el curso exista en el tenant y devuelve la coleccion de comisiones asignadas. */
public class GetCohortesByCursoQueryHandler : IRequestHandler<GetCohortesByCursoQuery, IReadOnlyList<CohorteDto>?>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly ICohorteRepository _cohorteRepository;

    public GetCohortesByCursoQueryHandler(
        ICursoRepository cursoRepository,
        ICohorteRepository cohorteRepository)
    {
        _cursoRepository = cursoRepository;
        _cohorteRepository = cohorteRepository;
    }

    public async Task<IReadOnlyList<CohorteDto>?> HandleAsync(GetCohortesByCursoQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Confirmar que el curso existe para el tenant actual (evita fuga de informacion)
        var curso = await _cursoRepository.GetByIdAsync(request.CursoId, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        // 2. Obtener las cohortes asociadas al curso
        var cohortes = await _cohorteRepository.GetByCursoIdAsync(request.CursoId, cancellationToken);

        // 3. Proyectar a DTOs de salida inmutables
        return cohortes.Select(c => new CohorteDto(
            c.Id,
            c.CursoId,
            c.DocenteId,
            c.Nombre,
            c.Periodo,
            c.Activo,
            c.FechaCreacionUtc
        )).ToList();
    }
}
