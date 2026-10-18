using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence.Configurations;

/* Configuración Fluent API para la entidad Curso.
Mapea la tabla "Cursos" en PostgreSQL asegurando tipos de datos nativos,
índices de unicidad por tenant y protección de concurrencia optimista. */
public class CursoConfiguration : IEntityTypeConfiguration<Curso>
{
    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        builder.ToTable("Cursos");

        builder.HasKey(c => c.Id);

        // Identificador multi-tenant para indexación y filtrado estricto.
        builder.Property(c => c.InstitucionId)
            .IsRequired();

        builder.Property(c => c.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Area)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        // Token de concurrencia optimista (Criterio A07) para prevenir colisiones de actualización.
        builder.Property(c => c.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(c => c.FechaCreacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.FechaModificacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Índice único compuesto: impide repetir el nombre de un curso dentro de la misma institución.
        builder.HasIndex(c => new { c.InstitucionId, c.Nombre })
            .IsUnique();

        // Relación 1 a N con Cohortes con borrado restringido para preservar historial académico.
        builder.HasMany(c => c.Cohortes)
            .WithOne(co => co.Curso)
            .HasForeignKey(co => co.CursoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
