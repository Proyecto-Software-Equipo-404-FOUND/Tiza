using Microsoft.EntityFrameworkCore;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence;

/* DbContext principal del microservicio PEI.Cursos.
Gestiona el acceso a PostgreSQL (pei_cursos_db), aplica las configuraciones Fluent API
y garantiza el aislamiento multi-tenant aplicando filtros globales de consulta en cada entidad. */
public class CursosDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Cohorte> Cohortes => Set<Cohorte>();
    public DbSet<Matriculacion> Matriculaciones => Set<Matriculacion>();

    /* Inyecta las opciones de configuración y el contexto multi-tenant.
    El parámetro opcional tenantContext permite que herramientas de diseño como
    las migraciones de EF Core puedan instanciar el contexto sin excepciones. */
    public CursosDbContext(
        DbContextOptions<CursosDbContext> options,
        ITenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las clases IEntityTypeConfiguration definidas en este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CursosDbContext).Assembly);

        /* Hallazgo H7: Cursos permitia consultas globales sin aislamiento si el contexto tenant era nulo.
        Se endurece el filtro global para que sea restrictivo y cerrado por defecto: si no existe un
        contexto tenant autenticado con un InstitucionId valido, las consultas devuelven 0 registros
        impidiendo cualquier fuga de informacion inadvertida entre inquilinos. */
        modelBuilder.Entity<Curso>()
            .HasQueryFilter(c => _tenantContext != null && _tenantContext.InstitucionId.HasValue && c.InstitucionId == _tenantContext.InstitucionId.Value);

        modelBuilder.Entity<Cohorte>()
            .HasQueryFilter(c => _tenantContext != null && _tenantContext.InstitucionId.HasValue && c.InstitucionId == _tenantContext.InstitucionId.Value);

        modelBuilder.Entity<Matriculacion>()
            .HasQueryFilter(m => _tenantContext != null && _tenantContext.InstitucionId.HasValue && m.InstitucionId == _tenantContext.InstitucionId.Value);
    }
}
