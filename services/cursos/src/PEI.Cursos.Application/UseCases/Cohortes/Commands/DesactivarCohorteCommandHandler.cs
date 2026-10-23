using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cohortes.Commands;

/* Manejador del comando DesactivarCohorteCommand.
Aplica la baja logica sobre la cohorte previa verificacion de pertenencia al curso y tenant. */
public class DesactivarCohorteCommandHandler : IRequestHandler<DesactivarCohorteCommand, bool>
{
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarCohorteCommandHandler(
        ICohorteRepository cohorteRepository,
        IUnitOfWork unitOfWork)
    {
        _cohorteRepository = cohorteRepository;
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

        // 2. Ejecutar la baja logica en la entidad (arroja DomainException si ya esta inactiva)
        cohorte.Desactivar();

        // 3. Confirmar la persistencia en PostgreSQL
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
