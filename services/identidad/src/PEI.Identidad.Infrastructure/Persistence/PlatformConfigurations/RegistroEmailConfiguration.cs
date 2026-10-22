using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence.PlatformConfigurations;

public class RegistroEmailConfiguration : IEntityTypeConfiguration<RegistroEmail>
{
    public void Configure(EntityTypeBuilder<RegistroEmail> builder)
    {
        builder.ToTable("RegistroEmail");

        builder.HasKey(r => r.EmailNormalizado);

        builder.Property(r => r.EmailNormalizado)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(r => r.TipoCuenta)
            .HasMaxLength(20)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RegistroEmail_TipoCuenta",
            "\"TipoCuenta\" IN ('Institucional', 'Plataforma')"));

        builder.Property(r => r.CuentaId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(r => new { r.TipoCuenta, r.CuentaId });
    }
}
