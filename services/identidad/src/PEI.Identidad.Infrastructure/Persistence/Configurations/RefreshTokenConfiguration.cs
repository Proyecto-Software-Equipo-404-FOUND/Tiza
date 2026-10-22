using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;


namespace PEI.Identidad.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UsuarioId)
            .IsRequired();

        builder.Property(t => t.Token)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(t => t.Token)
            .IsUnique();

        builder.Property(t => t.FechaExpiracion)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.Revocado)
            .IsRequired();

        builder.HasOne(t => t.Usuario)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
