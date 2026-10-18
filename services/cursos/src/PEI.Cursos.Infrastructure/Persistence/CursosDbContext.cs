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

        /* Filtros globales de consulta (Global Query Filters):
        Garantizan que cualquier SELECT sobre Cursos, Cohortes o Matriculaciones filtre
        automáticamente por el InstitucionId del token JWT actual, impidiendo fugas de datos. */
        modelBuilder.Entity<Curso>()
            .HasQueryFilter(c => _tenantContext == null || c.InstitucionId == _tenantContext.InstitucionId);

        modelBuilder.Entity<Cohorte>()
            .HasQueryFilter(c => _tenantContext == null || c.InstitucionId == _tenantContext.InstitucionId);

        modelBuilder.Entity<Matriculacion>()
            .HasQueryFilter(m => _tenantContext == null || m.InstitucionId == _tenantContext.InstitucionId);
    }
}
