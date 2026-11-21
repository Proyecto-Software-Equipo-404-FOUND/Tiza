using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.Interfaces;

namespace PEI.Cursos.Application.UseCases.Cursos.Commands;

/* Manejador del comando ActualizarCursoCommand.
Modifica los datos del curso validando su existencia en el tenant y la no duplicidad del nombre. */
public class ActualizarCursoCommandHandler : IRequestHandler<ActualizarCursoCommand, CursoDto?>
{
    private readonly ICursoRepository _cursoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarCursoCommandHandler(
        ICursoRepository cursoRepository,
        IUnitOfWork unitOfWork)
    {
        _cursoRepository = cursoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CursoDto?> HandleAsync(ActualizarCursoCommand request, CancellationToken cancellationToken = default)
    {
        // 1. Obtener la entidad filtrada por tenant mediante el repositorio
        var curso = await _cursoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        // 2. Si el nombre cambia, validar que no colisione con otro curso de la misma institucion
        var nombreTrim = request.Nombre.Trim();
        if (!curso.Nombre.Equals(nombreTrim, StringComparison.OrdinalIgnoreCase))
        {
            var yaExiste = await _cursoRepository.ExisteNombreAsync(nombreTrim, cancellationToken);
            if (yaExiste)
            {
                throw new InvalidOperationException($"Ya existe un curso con el nombre '{request.Nombre}' en la institucion.");
            }
        }

        // 3. Ejecutar la mutacion aplicando las invariantes de dominio
        curso.ActualizarDatos(request.Nombre, request.Area);

        // 4. Confirmar los cambios de forma atomica
        await _unitOfWork.CommitAsync(cancellationToken);

        // 5. Proyectar la entidad modificada al DTO inmutable
        return new CursoDto(
            curso.Id,
            curso.InstitucionId,
            curso.Nombre,
            curso.Area,
            curso.Activo,
            curso.FechaCreacionUtc
        );
    }
}
