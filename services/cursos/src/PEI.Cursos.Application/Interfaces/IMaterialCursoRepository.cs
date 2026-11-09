using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato de persistencia para la entidad MaterialCurso.
Aisla las operaciones sobre apuntes y materiales PDF en PostgreSQL (pei_cursos_db). */
public interface IMaterialCursoRepository
{
    // Obtiene un material por su identificador unico considerando el filtro multi-tenant.
    Task<MaterialCurso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Obtiene los materiales activos asociados a un curso ordenados del mas reciente al mas antiguo.
    Task<IReadOnlyList<MaterialCurso>> GetByCursoIdAsync(Guid cursoId, CancellationToken cancellationToken = default);

    // Registra una nueva ficha de material en el contexto transaccional.
    Task AddAsync(MaterialCurso material, CancellationToken cancellationToken = default);
}
