using System;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato de persistencia para la entidad Matriculacion.
Aisla las operaciones sobre inscripciones formales de alumnos en PostgreSQL (pei_cursos_db). */
public interface IMatriculacionRepository
{
    Task<Matriculacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExisteMatriculacionAsync(Guid cohorteId, Guid alumnoId, CancellationToken cancellationToken = default);
    Task AddAsync(Matriculacion matriculacion, CancellationToken cancellationToken = default);
}
