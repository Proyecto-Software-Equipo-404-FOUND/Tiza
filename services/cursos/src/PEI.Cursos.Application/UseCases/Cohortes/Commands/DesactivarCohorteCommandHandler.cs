using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Manejador del comando DesactivarCohorteCommand.
Aplica la baja logica sobre la cohorte previa verificacion de pertenencia al curso y tenant. */
public class DesactivarCohorteCommandHandler : IRequestHandler<DesactivarCohorteCommand, bool>
{
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IMatriculacionRepository _matriculacionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarCohorteCommandHandler(
        ICohorteRepository cohorteRepository,
        IMatriculacionRepository matriculacionRepository,
        IUnitOfWork unitOfWork)
    {
        _cohorteRepository = cohorteRepository;
        _matriculacionRepository = matriculacionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(DesactivarCohorteCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la cohorte verificando que pertenezca al curso solicitado
        var cohorte = await _cohorteRepository.GetByIdAsync(request.Id, cancellationToken);
        if (cohorte is null || cohorte.CursoId != request.CursoId)
        {
            return false;
        }

        // 2. No se da de baja una cohorte con alumnos cursando (mapea a 409 Conflict)
        var matriculasActivas = await _matriculacionRepository.GetByCohorteIdAsync(
            request.Id,
            soloActivas: true,
            cancellationToken
        );

        if (matriculasActivas.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se puede dar de baja la cohorte porque tiene {matriculasActivas.Count} alumno(s) matriculado(s). Dalos de baja primero.");
        }

        // 3. Ejecutar la baja logica en la entidad (arroja DomainException si ya esta inactiva)
        cohorte.Desactivar();

        // 4. Confirmar la persistencia en PostgreSQL
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
