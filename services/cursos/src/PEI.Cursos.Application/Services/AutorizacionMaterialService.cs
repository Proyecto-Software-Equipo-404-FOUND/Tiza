using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.Errors;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.Services;

/* Servicio de aplicacion que centraliza las reglas de autorizacion de materiales de estudio.
Encapsula la verificacion de roles (RN-MAT-01 / RN-MAT-02) y la regla anti-enumeracion (404 Not Found). */
public class AutorizacionMaterialService : IAutorizacionMaterialService
{
    private readonly ICursoRepository _cursoRepository;
    private readonly ICohorteRepository _cohorteRepository;
    private readonly ITenantContext _tenantContext;

    public AutorizacionMaterialService(
        ICursoRepository cursoRepository,
        ICohorteRepository cohorteRepository,
        ITenantContext tenantContext)
    {
        _cursoRepository = cursoRepository;
        _cohorteRepository = cohorteRepository;
        _tenantContext = tenantContext;
    }

    public async Task ValidarPermisoGestionAsync(Guid cursoId, CancellationToken cancellationToken = default)
    {
        // 1. Regla anti-enumeracion: Si el curso no existe, no pertenece al tenant o esta inactivo, responde 404
        var curso = await _cursoRepository.GetByIdAsync(cursoId, cancellationToken);
        if (curso is null || !curso.Activo)
        {
            throw new KeyNotFoundException($"El curso con ID '{cursoId}' no existe o no pertenece a la institucion.");
        }

        var rol = _tenantContext.Rol;

        // 2. Administrador tiene acceso total sobre los cursos de su institucion
        if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 3. Regla RN-MAT-02: El alumno tiene prohibido gestionar o acceder al material
        if (string.Equals(rol, "Alumno", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("No tenés permiso para gestionar el material de este curso.");
        }

        // 4. Regla RN-MAT-01: El docente solo puede gestionar el material si esta asignado a alguna cohorte activa del curso
        if (string.Equals(rol, "Docente", StringComparison.OrdinalIgnoreCase))
        {
            var usuarioId = _tenantContext.UsuarioId;
            if (!usuarioId.HasValue || usuarioId.Value == Guid.Empty)
            {
                throw new ForbiddenException("No tenés permiso para gestionar el material de este curso.");
            }

            var tieneCohorteActiva = await _cohorteRepository.ExisteDocenteEnCursoActivoAsync(
                cursoId,
                usuarioId.Value,
                cancellationToken
            );

            if (!tieneCohorteActiva)
            {
                throw new ForbiddenException("No tenés permiso para gestionar el material de este curso.");
            }

            return;
        }

        // Cualquier otro rol no reconocido es denegado
        throw new ForbiddenException("No tenés permiso para gestionar el material de este curso.");
    }

    public void ValidarAccesoTextoInterno()
    {
        // Regla RN-MAT-02: El alumno no tiene acceso al texto del apunte
        if (string.Equals(_tenantContext.Rol, "Alumno", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("El alumno no tiene permiso para acceder al material.");
        }
    }
}
