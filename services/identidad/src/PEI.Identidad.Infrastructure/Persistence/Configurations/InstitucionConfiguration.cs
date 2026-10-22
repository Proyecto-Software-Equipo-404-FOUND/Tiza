using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence.Configurations;

internal class InstitucionConfiguration : IEntityTypeConfiguration<Institucion>
{
    public void Configure(EntityTypeBuilder<Institucion> builder)
    {
        builder.ToTable("Institucion");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(i => i.Codigo)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(i => i.Codigo)
            .IsUnique();

        builder.Property(i => i.FechaCreacion)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(i => i.Activo)
            .IsRequired();

        builder.HasMany(i => i.Usuarios)
            .WithOne(u => u.Institucion)
            .HasForeignKey(u => u.InstitucionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
