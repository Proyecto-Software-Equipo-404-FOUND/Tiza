using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Application.Interfaces;

/* Contrato de persistencia para la entidad Curso.
Aisla las operaciones de consulta y almacenamiento sobre PostgreSQL (pei_cursos_db). */
public interface ICursoRepository
{
    Task<Curso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Curso> Items, int TotalItems)> GetPaginadosAsync(string? area, string? busqueda, int pagina, int tamanoPagina, CancellationToken cancellationToken = default);
    Task AddAsync(Curso curso, CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreAsync(string nombre, CancellationToken cancellationToken = default);
}
