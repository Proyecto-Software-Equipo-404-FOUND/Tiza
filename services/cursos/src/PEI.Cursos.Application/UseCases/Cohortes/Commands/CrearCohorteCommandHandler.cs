using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Manejador del comando CrearCohorteCommand.
Valida la existencia del curso en el tenant autenticado, la unicidad de comision por periodo y persiste la entidad. */
public class CrearCohorteCommandHandler : IRequestHandler<CrearCohorteCommand, CohorteDto>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public CrearCohorteCommandHandler(
        ICursoRepository cursoRepository,
        ICohorteRepository cohorteRepository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _cursoRepository = cursoRepository;
        _cohorteRepository = cohorteRepository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<CohorteDto> HandleAsync(CrearCohorteCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Validar la presencia del tenant en la sesion autenticada
        var institucionId = _tenantContext.InstitucionId;
        if (!institucionId.HasValue || institucionId.Value == Guid.Empty)
        {
            throw new DomainException("No se pudo identificar la institucion del usuario autenticado.");
        }

        // 2. Verificar existencia del curso asociado dentro de la institucion del inquilino
        var curso = await _cursoRepository.GetByIdAsync(request.CursoId, cancellationToken);
        if (curso is null)
        {
            // El contrato devuelve el mismo 404 para un curso inexistente o ajeno.
            throw new KeyNotFoundException("El curso especificado no existe o no pertenece a la institucion.");
        }

        if (!curso.Activo)
        {
            throw new DomainException("No es posible registrar cohortes en un curso inactivo.");
        }

        // 3. Validacion preventiva de unicidad para la tupla (CursoId, Periodo, Nombre) (mapea a 409 Conflict)
        var yaExiste = await _cohorteRepository.ExisteNombreEnPeriodoAsync(
            request.CursoId,
            request.Periodo,
            request.Nombre,
            cancellationToken
        );

        if (yaExiste)
        {
            throw new InvalidOperationException($"Ya existe una comision llamada '{request.Nombre}' para el ciclo '{request.Periodo}' en este curso.");
        }

        // 4. Instanciar la entidad de dominio Cohorte asegurando invariantes
        var cohorte = new Cohorte(
            institucionId.Value,
            request.CursoId,
            request.DocenteId,
            request.Nombre,
            request.Periodo
        );

        // 5. Persistir mediante el repositorio y confirmar con Unit of Work
        await _cohorteRepository.AddAsync(cohorte, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // 6. Proyectar a DTO de salida
        return new CohorteDto(
            cohorte.Id,
            cohorte.CursoId,
            cohorte.DocenteId,
            cohorte.Nombre,
            cohorte.Periodo,
            cohorte.Activo,
            cohorte.FechaCreacionUtc
        );
    }
}
