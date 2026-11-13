using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Commands;

/* Manejador del comando DarDeBajaMatriculacionCommand.
Ejecuta la baja logica de la matricula verificando su asociacion a la cohorte y tenant. */
public class DarDeBajaMatriculacionCommandHandler : IRequestHandler<DarDeBajaMatriculacionCommand, bool>
{
    private readonly IMatriculacionRepository _matriculacionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DarDeBajaMatriculacionCommandHandler(
        IMatriculacionRepository matriculacionRepository,
        IUnitOfWork unitOfWork)
    {
        _matriculacionRepository = matriculacionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(DarDeBajaMatriculacionCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la matriculacion y verificar correspondencia con la cohorte
        var matriculacion = await _matriculacionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (matriculacion is null || matriculacion.CohorteId != request.CohorteId)
        {
            return false;
        }

        // 2. Ejecutar la transicion de estado en el dominio
        matriculacion.DarDeBaja();

        // 3. Confirmar la persistencia transaccional
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
