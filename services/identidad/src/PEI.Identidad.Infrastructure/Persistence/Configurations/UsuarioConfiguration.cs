using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;


namespace PEI.Identidad.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.InstitucionId)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property<string>("EmailNormalizado")
            .HasMaxLength(150)
            .IsRequired()
            .HasComputedColumnSql("pei_normalize_email(\"Email\")", stored: true);

        builder.HasIndex("EmailNormalizado")
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(u => u.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Apellido)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Rol)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.FechaCreacion)
            .HasColumnType ("timestamp with time zone")
            .IsRequired();

        builder.Property(u => u.Activo)
            .IsRequired();

        builder.HasOne(u => u.Institucion)
            .WithMany(i => i.Usuarios)
            .HasForeignKey(u => u.InstitucionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.Usuario)
            .HasForeignKey(t =>  t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

