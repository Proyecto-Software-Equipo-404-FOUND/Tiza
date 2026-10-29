using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Exceptions;

namespace PEI.Cursos.Application.UseCases.Matriculaciones.Queries;

/* Manejador de la consulta GetMatriculacionesByCohorteQuery.
Valida que la cohorte exista y este activa para el tenant autenticado,
y retorna la lista de inscripciones proyectadas a DTOs de salida inmutables. */
public class GetMatriculacionesByCohorteQueryHandler : IRequestHandler<GetMatriculacionesByCohorteQuery, IReadOnlyList<MatriculacionDto>?>
{
    private readonly ICohorteRepository _cohorteRepository;
    private readonly IMatriculacionRepository _matriculacionRepository;

    public GetMatriculacionesByCohorteQueryHandler(
        ICohorteRepository cohorteRepository,
        IMatriculacionRepository matriculacionRepository)
    {
        _cohorteRepository = cohorteRepository;
        _matriculacionRepository = matriculacionRepository;
    }

    public async Task<IReadOnlyList<MatriculacionDto>?> HandleAsync(
        GetMatriculacionesByCohorteQuery request,
        CancellationToken cancellationToken = default)
    {
        if (request.CohorteId == Guid.Empty)
        {
            throw new DomainException("El identificador de la cohorte es obligatorio.");
        }

        // 1. Validar que la cohorte exista para el tenant y no este dada de baja
        var cohorte = await _cohorteRepository.GetByIdAsync(request.CohorteId, cancellationToken);
        if (cohorte is null || !cohorte.Activo)
        {
            return null;
        }

        // 2. Consultar las matriculaciones aplicando el filtro de estado correspondiente
        var soloActivas = request.SoloActivas ?? true;
        var matriculaciones = await _matriculacionRepository.GetByCohorteIdAsync(
            request.CohorteId,
            soloActivas,
            cancellationToken
        );

        // 3. Proyectar a DTOs inmutables de salida (lista vacia si no hay alumnos matriculados)
        return matriculaciones.Select(m => new MatriculacionDto(
            m.Id,
            m.InstitucionId,
            m.CohorteId,
            m.AlumnoId,
            m.FechaAlta,
            m.Estado.ToString()
        )).ToList();
    }
}
