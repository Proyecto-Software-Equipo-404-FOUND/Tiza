using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

public class EvaluacionDbContext : DbContext
{
    public EvaluacionDbContext(DbContextOptions<EvaluacionDbContext> options) : base(options)
    {
    }

    public DbSet<Evaluacion> Evaluaciones => Set<Evaluacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de la tabla Evaluacion
        modelBuilder.Entity<Evaluacion>(entity =>
        {
            entity.ToTable("Evaluacion");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstitucionId).IsRequired();
            entity.Property(e => e.PreguntasJson).IsRequired().HasColumnType("jsonb");

            entity.Property(e => e.Titulo).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.FechaCreacion).IsRequired();
            entity.Property(e => e.FechaActualizacion).IsRequired();

            // Clave para la concurrencia optimista (EV-04)
            entity.Property(e => e.Version).IsConcurrencyToken().IsRequired();
        });
    }
}
