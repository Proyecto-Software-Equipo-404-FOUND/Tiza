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

/* Implementacion del repositorio de MaterialCurso utilizando Entity Framework Core.
Maneja la persistencia y lectura de apuntes garantizando el aislamiento multi-tenant por DbContext. */
public class MaterialCursoRepository : IMaterialCursoRepository
{
    private readonly CursosDbContext _context;

    public MaterialCursoRepository(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<MaterialCurso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Materiales
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    /* Consulta los materiales activos de un curso ordenados cronologicamente de forma descendente.
    Utiliza AsNoTracking para maximizar el rendimiento al tratarse de un flujo de solo lectura. */
    public async Task<IReadOnlyList<MaterialCurso>> GetByCursoIdAsync(Guid cursoId, CancellationToken cancellationToken = default)
    {
        return await _context.Materiales
            .AsNoTracking()
            .Where(m => m.CursoId == cursoId && m.Activo)
            .OrderByDescending(m => m.FechaSubidaUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MaterialCurso material, CancellationToken cancellationToken = default)
    {
        await _context.Materiales.AddAsync(material, cancellationToken);
    }
}
