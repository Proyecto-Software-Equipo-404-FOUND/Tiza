using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Manejador del comando MatricularAlumnoCommand (Caso de Uso CU-04).
Orquesta la validación del tenant, la verificación de la cohorte activa,
la prevención de doble matriculación y la persistencia atómica. */
public class MatricularAlumnoCommandHandler : IRequestHandler<MatricularAlumnoCommand, MatriculacionDto>
{
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IMatriculacionRepository _matriculacionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public MatricularAlumnoCommandHandler(
        ICohorteRepository cohorteRepository,
        IMatriculacionRepository matriculacionRepository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _cohorteRepository = cohorteRepository;
        _matriculacionRepository = matriculacionRepository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<MatriculacionDto> HandleAsync(MatricularAlumnoCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Validar identidad institucional desde el contexto autenticado
        var institucionId = _tenantContext.InstitucionId;
        if (!institucionId.HasValue || institucionId.Value == Guid.Empty)
        {
            throw new DomainException("No se pudo identificar la institucion del usuario autenticado.");
        }

        // 2. Validar que la cohorte exista para el tenant (404 anti-enumeracion si no existe)
        var cohorte = await _cohorteRepository.GetByIdAsync(request.CohorteId, cancellationToken);
        if (cohorte is null)
        {
            throw new KeyNotFoundException($"La cohorte con ID '{request.CohorteId}' no existe o no pertenece a la institucion.");
        }

        // 3. Validar estado operativo de la comision
        if (!cohorte.Activo)
        {
            throw new DomainException("No es posible matricular alumnos en una cohorte inactiva.");
        }

        // 4. Validar identificador del alumno previo a instanciar la entidad
        if (request.AlumnoId == Guid.Empty)
        {
            throw new DomainException("El alumno es obligatorio para la matriculacion.");
        }

        // 5. Validacion preventiva de unicidad para proteger el indice compuesto
        var yaMatriculado = await _matriculacionRepository.ExisteMatriculacionAsync(request.CohorteId, request.AlumnoId, cancellationToken);
        if (yaMatriculado)
        {
            throw new InvalidOperationException($"El alumno ya se encuentra matriculado en la cohorte '{cohorte.Nombre}'.");
        }

        // 6. Instanciacion de la entidad pura de dominio
        var matriculacion = new Matriculacion(institucionId.Value, request.CohorteId, request.AlumnoId);

        // 7. Persistencia y confirmacion transaccional
        await _matriculacionRepository.AddAsync(matriculacion, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // 8. Proyeccion al DTO inmutable de salida
        return new MatriculacionDto(
            matriculacion.Id,
            matriculacion.InstitucionId,
            matriculacion.CohorteId,
            matriculacion.AlumnoId,
            matriculacion.FechaAlta,
            matriculacion.Estado.ToString()
        );
    }
}
