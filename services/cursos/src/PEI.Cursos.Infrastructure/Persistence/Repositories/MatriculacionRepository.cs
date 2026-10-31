using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Infrastructure.Persistence;

namespace PEI.Cursos.Infrastructure.Persistence.Repositories;

/* Implementacion del repositorio de Matriculaciones con Entity Framework Core.
Maneja la persistencia y lectura de inscripciones garantizando el aislamiento multi-tenant. */
public class MatriculacionRepository : IMatriculacionRepository
{
    private readonly CursosDbContext _context;

    public MatriculacionRepository(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<Matriculacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Matriculaciones
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<bool> ExisteMatriculacionAsync(
        Guid cohorteId,
        Guid alumnoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Matriculaciones
            .AnyAsync(m => m.CohorteId == cohorteId && m.AlumnoId == alumnoId, cancellationToken);
    }

    public async Task AddAsync(Matriculacion matriculacion, CancellationToken cancellationToken = default)
    {
        await _context.Matriculaciones.AddAsync(matriculacion, cancellationToken);
    }

    /* Recupera las matriculaciones de una cohorte con AsNoTracking para optimizar la consulta de lectura.
    Aplica el filtro de estado si se solicita y aprovecha el Global Query Filter multi-tenant de CursosDbContext. */
    public async Task<IReadOnlyList<Matriculacion>> GetByCohorteIdAsync(
        Guid cohorteId,
        bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Matriculaciones
            .AsNoTracking()
            .Where(m => m.CohorteId == cohorteId);

        if (soloActivas)
        {
            query = query.Where(m => m.Estado == EstadoMatriculacion.Activa);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
