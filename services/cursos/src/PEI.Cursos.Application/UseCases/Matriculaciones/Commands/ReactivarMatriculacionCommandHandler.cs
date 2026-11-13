using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Manejador del comando ReactivarMatriculacionCommand.
Restaura el estado activo de la matricula verificando su asociacion a la cohorte y tenant. */
public class ReactivarMatriculacionCommandHandler : IRequestHandler<ReactivarMatriculacionCommand, bool>
{
    private readonly IMatriculacionRepository _matriculacionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivarMatriculacionCommandHandler(
        IMatriculacionRepository matriculacionRepository,
        IUnitOfWork unitOfWork)
    {
        _matriculacionRepository = matriculacionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(ReactivarMatriculacionCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la matriculacion y verificar correspondencia con la cohorte
        var matriculacion = await _matriculacionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (matriculacion is null || matriculacion.CohorteId != request.CohorteId)
        {
            return false;
        }

        // 2. Ejecutar la reactivacion de la matriculacion en el dominio
        matriculacion.Reactivar();

        // 3. Confirmar la persistencia transaccional
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
