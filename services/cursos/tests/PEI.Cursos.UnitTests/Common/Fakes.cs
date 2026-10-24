using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.UnitTests.Common;

/* Dobles de prueba (fakes en memoria) para simular dependencias de infraestructura.
Permiten ejecutar pruebas unitarias ultra rapidas sin requerir base de datos PostgreSQL. */

public class FakeTenantContext : ITenantContext
{
    public Guid? InstitucionId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? Rol { get; set; }
    public bool EstaAutenticado => UsuarioId.HasValue;
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int CommitsRealizados { get; private set; }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        CommitsRealizados++;
        return Task.FromResult(1);
    }
}

public class FakeCursoRepository : ICursoRepository
{
    public List<Curso> Items { get; } = new();

    public Task<Curso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var curso = Items.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(curso);
    }

    public Task<(IReadOnlyList<Curso> Items, int TotalItems)> GetPaginadosAsync(
        string? area,
        string? busqueda,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = Items.AsQueryable();

        if (!string.IsNullOrWhiteSpace(area))
        {
            var areaNorm = area.Trim().ToLower();
            query = query.Where(c => c.Area.ToLower() == areaNorm);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var searchNorm = busqueda.Trim().ToLower();
            query = query.Where(c => c.Nombre.ToLower().Contains(searchNorm));
        }

        var total = query.Count();
        var paginaSegura = pagina < 1 ? 1 : pagina;
        var tamanoSeguro = tamanoPagina <= 0 ? 10 : tamanoPagina;

        var pagedItems = query
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToList();

        return Task.FromResult(((IReadOnlyList<Curso>)pagedItems, total));
    }

    public Task AddAsync(Curso curso, CancellationToken cancellationToken = default)
    {
        Items.Add(curso);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteNombreAsync(string nombre, CancellationToken cancellationToken = default)
    {
        var norm = nombre.Trim().ToLower();
        var existe = Items.Any(c => c.Nombre.ToLower() == norm);
        return Task.FromResult(existe);
    }
}

public class FakeCohorteRepository : ICohorteRepository
{
    public List<Cohorte> Items { get; } = new();

    public Task<Cohorte?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cohorte = Items.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(cohorte);
    }

    public Task<IReadOnlyList<Cohorte>> GetByCursoIdAsync(Guid cursoId, CancellationToken cancellationToken = default)
    {
        var list = Items.Where(c => c.CursoId == cursoId).ToList();
        return Task.FromResult((IReadOnlyList<Cohorte>)list);
    }

    public Task AddAsync(Cohorte cohorte, CancellationToken cancellationToken = default)
    {
        Items.Add(cohorte);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteNombreEnPeriodoAsync(
        Guid cursoId,
        string periodo,
        string nombre,
        CancellationToken cancellationToken = default)
    {
        var pNorm = periodo.Trim().ToLower();
        var nNorm = nombre.Trim().ToLower();
        var existe = Items.Any(c => c.CursoId == cursoId && c.Periodo.ToLower() == pNorm && c.Nombre.ToLower() == nNorm);
        return Task.FromResult(existe);
    }

    public Task<bool> ExisteNombreEnPeriodoAsync(
        Guid cursoId,
        string periodo,
        string nombre,
        Guid cohorteIdExcluir,
        CancellationToken cancellationToken = default)
    {
        var pNorm = periodo.Trim().ToLower();
        var nNorm = nombre.Trim().ToLower();
        var existe = Items.Any(c => c.CursoId == cursoId && c.Periodo.ToLower() == pNorm && c.Nombre.ToLower() == nNorm && c.Id != cohorteIdExcluir);
        return Task.FromResult(existe);
    }
}

public class FakeMatriculacionRepository : IMatriculacionRepository
{
    public List<Matriculacion> Items { get; } = new();

    public Task<Matriculacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var matriculacion = Items.FirstOrDefault(m => m.Id == id);
        return Task.FromResult(matriculacion);
    }

    public Task<bool> ExisteMatriculacionAsync(Guid cohorteId, Guid alumnoId, CancellationToken cancellationToken = default)
    {
        var existe = Items.Any(m => m.CohorteId == cohorteId && m.AlumnoId == alumnoId);
        return Task.FromResult(existe);
    }

    public Task AddAsync(Matriculacion matriculacion, CancellationToken cancellationToken = default)
    {
        Items.Add(matriculacion);
        return Task.CompletedTask;
    }
}
