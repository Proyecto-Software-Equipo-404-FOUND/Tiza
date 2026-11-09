using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence.Configurations;

/* Configuracion Fluent API para la entidad MaterialCurso.
Mapea la tabla "Materiales" en PostgreSQL (pei_cursos_db), configurando tipos de columna,
longitudes maximas, indices de busqueda y la relacion N:1 con la entidad Curso. */
public class MaterialCursoConfiguration : IEntityTypeConfiguration<MaterialCurso>
{
    public void Configure(EntityTypeBuilder<MaterialCurso> builder)
    {
        builder.ToTable("Materiales");

        builder.HasKey(m => m.Id);

        // Identificador multi-tenant para aislamiento de datos.
        builder.Property(m => m.InstitucionId)
            .IsRequired();

        // Clave foranea fisica hacia Curso.
        builder.Property(m => m.CursoId)
            .IsRequired();

        builder.Property(m => m.Titulo)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(m => m.NombreArchivo)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(m => m.TamanoBytes)
            .IsRequired();

        builder.Property(m => m.CantidadPaginas)
            .IsRequired();

        builder.Property(m => m.UbicacionArchivo)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.TextoExtraido)
            .HasColumnType("text")
            .IsRequired();

        // Conversion del Enum EstadoMaterialCurso a string legible en la columna VARCHAR(30) de PostgreSQL.
        builder.Property(m => m.Estado)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Referencia logica al usuario docente/admin que subio el material (claim sub).
        builder.Property(m => m.SubidoPorId)
            .IsRequired();

        builder.Property(m => m.FechaSubidaUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.Activo)
            .IsRequired();

        // Token de concurrencia optimista (Criterio A07 de catedra).
        builder.Property(m => m.Version)
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(m => m.FechaCreacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.FechaModificacionUtc)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Indice compuesto para optimizar las consultas de listado por curso y estado activo dentro del tenant.
        builder.HasIndex(m => new { m.InstitucionId, m.CursoId, m.Activo });

        // Relacion N a 1 con Curso.
        builder.HasOne(m => m.Curso)
            .WithMany(c => c.Materiales)
            .HasForeignKey(m => m.CursoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
