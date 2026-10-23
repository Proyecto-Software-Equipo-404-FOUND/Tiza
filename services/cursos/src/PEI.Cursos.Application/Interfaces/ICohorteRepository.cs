using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato de persistencia para la entidad Cohorte.
Aisla las operaciones sobre comisiones y divisiones academicas en PostgreSQL (pei_cursos_db). */
public interface ICohorteRepository
{
    Task<Cohorte?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Cohorte>> GetByCursoIdAsync(Guid cursoId, CancellationToken cancellationToken = default);
    Task AddAsync(Cohorte cohorte, CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreEnPeriodoAsync(Guid cursoId, string periodo, string nombre, CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreEnPeriodoAsync(Guid cursoId, string periodo, string nombre, Guid cohorteIdExcluir, CancellationToken cancellationToken = default);
}
