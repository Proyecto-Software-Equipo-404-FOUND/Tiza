using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Manejador del comando CrearCursoCommand.
Orquesta la validacion de tenant, la prevencion de duplicados y la persistencia atomica. */
public class CrearCursoCommandHandler : IRequestHandler<CrearCursoCommand, CursoDto>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public CrearCursoCommandHandler(
        ICursoRepository cursoRepository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _cursoRepository = cursoRepository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<CursoDto> HandleAsync(CrearCursoCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener y validar el tenant desde el contexto de ejecucion autenticado
        var institucionId = _tenantContext.InstitucionId;
        if (!institucionId.HasValue || institucionId.Value == Guid.Empty)
        {
            throw new DomainException("No se pudo identificar la institucion del usuario autenticado.");
        }

        // 2. Validacion preventiva de unicidad (mapea a 409 Conflict en ProblemDetailsMiddleware)
        var existeNombre = await _cursoRepository.ExisteNombreAsync(request.Nombre, cancellationToken);
        if (existeNombre)
        {
            throw new InvalidOperationException($"Ya existe un curso con el nombre '{request.Nombre}' en la institucion.");
        }

        // 3. Creacion de la entidad pura de dominio aplicando sus invariantes
        var curso = new Curso(institucionId.Value, request.Nombre, request.Area);

        // 4. Registro en el repositorio y confirmacion transaccional
        await _cursoRepository.AddAsync(curso, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // 5. Proyeccion al DTO de salida
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
