using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Queries;

/* Manejador de la consulta GetCursoByIdQuery.
Busca la entidad de curso a traves del repositorio respetando el aislamiento multi-tenant. */
public class GetCursoByIdQueryHandler : IRequestHandler<GetCursoByIdQuery, CursoDto?>
{
    private readonly ICursoRepository _cursoRepository;

    public GetCursoByIdQueryHandler(ICursoRepository cursoRepository)
    {
        _cursoRepository = cursoRepository;
    }

    public async Task<CursoDto?> HandleAsync(GetCursoByIdQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Consultar el curso en la base de datos aplicando el filtro global de tenant
        var curso = await _cursoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        // 2. Mapear la entidad de dominio a DTO inmutable
        return new CursoDto(
            curso.Id,
            curso.InstitucionId,
            curso.Nombre,
            curso.Area,
            curso.Activo,
            curso.FechaCreacionUtc
        );
    }
}
