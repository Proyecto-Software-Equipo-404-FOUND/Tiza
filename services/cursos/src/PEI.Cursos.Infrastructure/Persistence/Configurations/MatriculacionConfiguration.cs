using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence.Configurations;

/* Configuración Fluent API para la entidad Matriculacion.
Mapea la tabla "Matriculaciones" en PostgreSQL, persistiendo el enum EstadoMatriculacion
como VARCHAR(30) y asegurando que un alumno no se matricule dos veces en la misma cohorte. */
public class MatriculacionConfiguration : IEntityTypeConfiguration<Matriculacion>
{
    public void Configure(EntityTypeBuilder<Matriculacion> builder)
    {
        builder.ToTable("Matriculaciones");

        builder.HasKey(m => m.Id);

        // Identificador multi-tenant para aislamiento de datos.
        builder.Property(m => m.InstitucionId)
            .IsRequired();

        // Clave foránea física hacia Cohorte.
        builder.Property(m => m.CohorteId)
            .IsRequired();

        // Referencia lógica al usuario alumno en Identidad_DB (sin FK física por Database-per-Service).
        builder.Property(m => m.AlumnoId)
            .IsRequired();

        builder.Property(m => m.FechaAlta)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Conversión del Enum de Dominio a string legible en la columna VARCHAR(30) de PostgreSQL.
        builder.Property(m => m.Estado)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Token de concurrencia optimista (Criterio A07).
        builder.Property(m => m.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(m => m.FechaCreacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.FechaModificacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Índice único compuesto: impide la doble matriculación de un alumno en una misma cohorte.
        builder.HasIndex(m => new { m.CohorteId, m.AlumnoId })
            .IsUnique();

        // Relación N a 1 con Cohorte.
        builder.HasOne(m => m.Cohorte)
            .WithMany(c => c.Matriculaciones)
            .HasForeignKey(m => m.CohorteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
