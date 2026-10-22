using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

/* Manejador de la consulta GetMatriculacionByIdQuery.
Obtiene los datos de matriculación proyectados al DTO inmutable para la API. */
public class GetMatriculacionByIdQueryHandler : IRequestHandler<GetMatriculacionByIdQuery, MatriculacionDto?>
{
    private readonly IMatriculacionRepository _matriculacionRepository;

    public GetMatriculacionByIdQueryHandler(IMatriculacionRepository matriculacionRepository)
    {
        _matriculacionRepository = matriculacionRepository;
    }

    public async Task<MatriculacionDto?> HandleAsync(GetMatriculacionByIdQuery request, CancellationToken cancellationToken = default)
    {
        var matriculacion = await _matriculacionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (matriculacion is null)
        {
            return null;
        }

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
