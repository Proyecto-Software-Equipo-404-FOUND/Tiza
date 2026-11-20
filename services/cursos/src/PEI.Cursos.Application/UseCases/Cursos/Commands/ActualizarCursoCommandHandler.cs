using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Manejador del comando ActualizarCursoCommand.
Modifica los datos del curso validando su existencia en el tenant y la no duplicidad del nombre. */
public class ActualizarCursoCommandHandler : IRequestHandler<ActualizarCursoCommand, CursoDto?>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICohorteRepository? _cohorteRepository;
    private readonly ITenantContext? _tenantContext;

    public ActualizarCursoCommandHandler(
        ICursoRepository cursoRepository,
        IUnitOfWork unitOfWork)
        : this(cursoRepository, unitOfWork, null, null)
    {
    }

    public ActualizarCursoCommandHandler(
        ICursoRepository cursoRepository,
        IUnitOfWork unitOfWork,
        ICohorteRepository? cohorteRepository,
        ITenantContext? tenantContext)
    {
        _cursoRepository = cursoRepository;
        _unitOfWork = unitOfWork;
        _cohorteRepository = cohorteRepository;
        _tenantContext = tenantContext;
    }

    public async Task<CursoDto?> HandleAsync(ActualizarCursoCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la entidad filtrada por tenant mediante el repositorio
        var curso = await _cursoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        // 2. Regla anti-enumeracion: Si el rol es Docente, solo puede editar cursos donde dicta comisiones
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

        // 3. Si el nombre cambia, validar que no colisione con otro curso de la misma institucion
        var nombreTrim = request.Nombre.Trim();
        if (!curso.Nombre.Equals(nombreTrim, StringComparison.OrdinalIgnoreCase))
        {
            var yaExiste = await _cursoRepository.ExisteNombreAsync(nombreTrim, cancellationToken);
            if (yaExiste)
            {
                throw new InvalidOperationException($"Ya existe un curso con el nombre '{request.Nombre}' en la institucion.");
            }
        }

        // 3. Ejecutar la mutacion aplicando las invariantes de dominio
        curso.ActualizarDatos(request.Nombre, request.Area);

        // 4. Confirmar los cambios de forma atomica
        await _unitOfWork.CommitAsync(cancellationToken);

        // 5. Proyectar la entidad modificada al DTO inmutable
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
