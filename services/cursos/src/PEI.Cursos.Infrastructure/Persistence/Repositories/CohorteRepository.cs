using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Infrastructure.Persistence;

namespace PEI.Cursos.Infrastructure.Persistence.Repositories;

/* Implementacion del repositorio de Cohortes con Entity Framework Core.
Maneja la persistencia y lectura de comisiones garantizando el aislamiento multi-tenant. */
public class CohorteRepository : ICohorteRepository
{
    private readonly CursosDbContext _context;

    public CohorteRepository(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<Cohorte?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Cohortes
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Cohorte>> GetByCursoIdAsync(Guid cursoId, CancellationToken cancellationToken = default)
    {
        return await _context.Cohortes
            .AsNoTracking()
            .Where(c => c.CursoId == cursoId)
            .OrderBy(c => c.Periodo)
            .ThenBy(c => c.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Cohorte cohorte, CancellationToken cancellationToken = default)
    {
        await _context.Cohortes.AddAsync(cohorte, cancellationToken);
    }

    public async Task<bool> ExisteNombreEnPeriodoAsync(
        Guid cursoId,
        string periodo,
        string nombre,
        CancellationToken cancellationToken = default)
    {
        var periodoNormalizado = periodo.Trim().ToLower();
        var nombreNormalizado = nombre.Trim().ToLower();

        return await _context.Cohortes
            .AnyAsync(c => c.CursoId == cursoId
                        && c.Periodo.ToLower() == periodoNormalizado
                        && c.Nombre.ToLower() == nombreNormalizado,
                      cancellationToken);
    }
}
