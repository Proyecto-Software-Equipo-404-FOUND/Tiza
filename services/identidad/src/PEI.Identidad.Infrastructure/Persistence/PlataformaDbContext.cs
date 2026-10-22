using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence;

public class PlataformaDbContext : DbContext
{
    public PlataformaDbContext(
        DbContextOptions<PlataformaDbContext> options)
        : base(options)
    {
    }

    public DbSet<CuentaPlataforma> CuentasPlataforma =>
        Set<CuentaPlataforma>();

    public DbSet<RegistroEmail> RegistrosEmail =>
    Set<RegistroEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDbFunction(typeof(EmailDbFunctions).GetMethod(nameof(EmailDbFunctions.Normalize))!)
            .HasName("pei_normalize_email");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PlataformaDbContext).Assembly,
            type => type.Namespace ==
                "PEI.Identidad.Infrastructure.Persistence.PlatformConfigurations");
    }
}
