using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Manejador del comando ActualizarCohorteCommand.
Valida la existencia de la cohorte en el curso y tenant, verifica que no colisione
la tupla (CursoId, Periodo, Nombre) y aplica la modificacion de datos. */
public class ActualizarCohorteCommandHandler : IRequestHandler<ActualizarCohorteCommand, CohorteDto?>
{
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarCohorteCommandHandler(
        ICohorteRepository cohorteRepository,
        IUnitOfWork unitOfWork)
    {
        _cohorteRepository = cohorteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CohorteDto?> HandleAsync(ActualizarCohorteCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la entidad filtrada por tenant mediante el repositorio
        var cohorte = await _cohorteRepository.GetByIdAsync(request.Id, cancellationToken);
        if (cohorte is null || cohorte.CursoId != request.CursoId)
        {
            return null;
        }

        // 2. Si el nombre o periodo cambian, validar que no colisione con otra cohorte del mismo curso
        var yaExisteDuplicado = await _cohorteRepository.ExisteNombreEnPeriodoAsync(
            request.CursoId,
            request.Periodo,
            request.Nombre,
            cohorte.Id,
            cancellationToken
        );

        if (yaExisteDuplicado)
        {
            throw new InvalidOperationException($"Ya existe una cohorte con el nombre '{request.Nombre}' para el período '{request.Periodo}' en este curso.");
        }

        // 3. Ejecutar las mutaciones aplicando las invariantes de dominio
        cohorte.ActualizarDatos(request.Nombre, request.Periodo);
        if (cohorte.DocenteId != request.DocenteId)
        {
            cohorte.ReasignarDocente(request.DocenteId);
        }

        // 4. Confirmar los cambios de forma atomica en la base de datos
        await _unitOfWork.CommitAsync(cancellationToken);

        // 5. Proyectar la entidad modificada al DTO inmutable
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
