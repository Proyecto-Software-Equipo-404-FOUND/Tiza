using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence.Configurations;

/* Configuración Fluent API para la entidad Cohorte.
Mapea la tabla "Cohortes" en PostgreSQL vinculándola con Curso y restringiendo
la duplicidad de comisiones mediante un índice compuesto (CursoId, Periodo, Nombre). */
public class CohorteConfiguration : IEntityTypeConfiguration<Cohorte>
{
    public void Configure(EntityTypeBuilder<Cohorte> builder)
    {
        builder.ToTable("Cohortes");

        builder.HasKey(c => c.Id);

        // Identificador multi-tenant para aislamiento de datos.
        builder.Property(c => c.InstitucionId)
            .IsRequired();

        // Clave foránea física hacia Curso.
        builder.Property(c => c.CursoId)
            .IsRequired();

        // Referencia lógica al usuario docente en Identidad_DB (sin FK física por Database-per-Service).
        builder.Property(c => c.DocenteId)
            .IsRequired();

        builder.Property(c => c.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Periodo)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        // Token de concurrencia optimista (Criterio A07).
        builder.Property(c => c.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(c => c.FechaCreacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.FechaModificacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Índice único compuesto: impide dos comisiones con el mismo nombre en el mismo período para un curso.
        builder.HasIndex(c => new { c.CursoId, c.Periodo, c.Nombre })
            .IsUnique();

        // Relación N a 1 con Curso.
        builder.HasOne(c => c.Curso)
            .WithMany(cu => cu.Cohortes)
            .HasForeignKey(c => c.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación 1 a N con Matriculaciones con borrado restringido.
        builder.HasMany(c => c.Matriculaciones)
            .WithOne(m => m.Cohorte)
            .HasForeignKey(m => m.CohorteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
