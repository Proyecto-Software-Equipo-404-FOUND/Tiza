using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Materiales.Commands;

/* Manejador del comando EliminarMaterialCommand.
Aplica la regla de baja logica (RN-MAT-05) verificando que el usuario tenga permisos
sobre el curso y preservando el documento fisico en disco para consistencia con evaluaciones historicas. */
public class EliminarMaterialCommandHandler : IRequestHandler<EliminarMaterialCommand, bool>
{
    private readonly IMaterialCursoRepository _materialRepository;
    private readonly IAutorizacionMaterialService _autorizacionService;
    private readonly IUnitOfWork _unitOfWork;

    public EliminarMaterialCommandHandler(
        IMaterialCursoRepository materialRepository,
        IAutorizacionMaterialService autorizacionService,
        IUnitOfWork unitOfWork)
    {
        _materialRepository = materialRepository;
        _autorizacionService = autorizacionService;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(EliminarMaterialCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Validar permisos de gestion sobre el curso (404 si el curso no existe / 403 si no tiene permiso)
        await _autorizacionService.ValidarPermisoGestionAsync(request.CursoId, cancellationToken);

        // 2. Obtener el material y verificar pertenencia al curso y estado activo
        var material = await _materialRepository.GetByIdAsync(request.Id, cancellationToken);
        if (material is null || !material.Activo || material.CursoId != request.CursoId)
        {
            throw new KeyNotFoundException($"El material con ID '{request.Id}' no existe o no pertenece al curso.");
        }

        // 3. Ejecutar la baja logica
        material.Desactivar();

        // 4. Confirmar cambios transaccionales
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
