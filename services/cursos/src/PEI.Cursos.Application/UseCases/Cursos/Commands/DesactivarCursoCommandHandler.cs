using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Manejador del comando DesactivarCursoCommand.
Aplica la baja logica sobre el curso previa verificacion de existencia en el tenant. */
public class DesactivarCursoCommandHandler : IRequestHandler<DesactivarCursoCommand, bool>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly IMatriculacionRepository _matriculacionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarCursoCommandHandler(
        ICursoRepository cursoRepository,
        IMatriculacionRepository matriculacionRepository,
        IUnitOfWork unitOfWork)
    {
        _cursoRepository = cursoRepository;
        _matriculacionRepository = matriculacionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(DesactivarCursoCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Verificar existencia del curso dentro del tenant del usuario
        var curso = await _cursoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (curso is null)
        {
            return false;
        }

        // 2. Regla de negocio: No se puede dar de baja un curso con alumnos activos en sus comisiones (mapea a 409 Conflict)
        var tieneAlumnosActivos = await _matriculacionRepository.ExisteActivaEnCursoAsync(request.Id, cancellationToken);
        if (tieneAlumnosActivos)
        {
            throw new CursoConAlumnosException();
        }

        // 3. Ejecutar la baja logica en la entidad (arroja DomainException si ya esta inactivo)
        curso.Desactivar();

        // 4. Persistir los cambios en PostgreSQL
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
