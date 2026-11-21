using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Queries;

/* Manejador de la consulta GetCursoByIdQuery.
Busca la entidad de curso a traves del repositorio respetando el aislamiento multi-tenant. */
public class GetCursoByIdQueryHandler : IRequestHandler<GetCursoByIdQuery, CursoDto?>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly ICohorteRepository? _cohorteRepository;
    private readonly ITenantContext? _tenantContext;

    public GetCursoByIdQueryHandler(ICursoRepository cursoRepository)
        : this(cursoRepository, null, null)
    {
    }

    public GetCursoByIdQueryHandler(
        ICursoRepository cursoRepository,
        ICohorteRepository? cohorteRepository,
        ITenantContext? tenantContext)
    {
        _cursoRepository = cursoRepository;
        _cohorteRepository = cohorteRepository;
        _tenantContext = tenantContext;
    }

    public async Task<CursoDto?> HandleAsync(GetCursoByIdQuery request, CancellationToken cancellationToken = default)
    {
        // 1. Consultar el curso en la base de datos aplicando el filtro global de tenant
        var curso = await _cursoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        // 2. Regla anti-enumeracion: Si el rol es Docente, solo puede ver cursos donde dicta comisiones
        if (_tenantContext is not null &&
            string.Equals(_tenantContext.Rol, "Docente", StringComparison.OrdinalIgnoreCase))
        {
            if (_tenantContext.UsuarioId.HasValue && _cohorteRepository is not null)
            {
                var dictaCurso = await _cohorteRepository.ExisteDocenteEnCursoAsync(
                    curso.Id,
                    _tenantContext.UsuarioId.Value,
                    cancellationToken
                );

                if (!dictaCurso)
                {
                    return null;
                }
            }
        }

        // 3. Mapear la entidad de dominio a DTO inmutable
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
