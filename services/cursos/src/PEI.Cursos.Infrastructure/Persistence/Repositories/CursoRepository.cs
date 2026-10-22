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

/* Implementacion del repositorio de Cursos utilizando Entity Framework Core.
Aprovecha el Global Query Filter configurado en CursosDbContext para filtrar automaticamente por tenant. */
public class CursoRepository : ICursoRepository
{
    private readonly CursosDbContext _context;

    public CursoRepository(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<Curso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Cursos
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Curso> Items, int TotalItems)> GetPaginadosAsync(
        string? area,
        string? busqueda,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Cursos
            .AsNoTracking()
            .AsQueryable();

        // 1. Filtro opcional por area tematica
        if (!string.IsNullOrWhiteSpace(area))
        {
            var areaNormalizada = area.Trim().ToLower();
            query = query.Where(c => c.Area.ToLower() == areaNormalizada);
        }

        // 2. Filtro opcional por busqueda parcial de texto en el nombre
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var busquedaNormalizada = busqueda.Trim().ToLower();
            query = query.Where(c => c.Nombre.ToLower().Contains(busquedaNormalizada));
        }

        // 3. Orden alfabetico predeterminado
        query = query.OrderBy(c => c.Nombre);

        // 4. Conteo total de coincidencias para el calculo de paginacion
        var totalItems = await query.CountAsync(cancellationToken);

        // 5. Normalizacion de limites de navegacion seguros
        var paginaSegura = pagina < 1 ? 1 : pagina;
        var tamanoSeguro = tamanoPagina <= 0 ? 10 : (tamanoPagina > 50 ? 50 : tamanoPagina);

        // 6. Extraccion paginada de registros en memoria
        var items = await query
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public async Task AddAsync(Curso curso, CancellationToken cancellationToken = default)
    {
        await _context.Cursos.AddAsync(curso, cancellationToken);
    }

    public async Task<bool> ExisteNombreAsync(string nombre, CancellationToken cancellationToken = default)
    {
        var nombreNormalizado = nombre.Trim().ToLower();
        return await _context.Cursos
            .AnyAsync(c => c.Nombre.ToLower() == nombreNormalizado, cancellationToken);
    }
}
