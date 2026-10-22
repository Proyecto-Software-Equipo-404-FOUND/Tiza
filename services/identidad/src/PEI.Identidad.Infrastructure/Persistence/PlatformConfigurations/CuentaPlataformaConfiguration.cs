
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence.PlatformConfigurations;

public class CuentaPlataformaConfiguration
    : IEntityTypeConfiguration<CuentaPlataforma>
{
    public void Configure(EntityTypeBuilder<CuentaPlataforma> builder)
    {
        builder.ToTable("CuentaPlataforma");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Email)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property<string>("EmailNormalizado")
            .HasMaxLength(150)
            .IsRequired()
            .HasComputedColumnSql(
                "pei_normalize_email(\"Email\")",
                stored: true);

        builder.HasIndex("EmailNormalizado")
            .IsUnique();

        builder.Property(c => c.PasswordHash)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Apellido)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Rol)
            .HasMaxLength(30)
            .HasDefaultValue("AdministradorGlobal")
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CuentaPlataforma_Rol",
            "\"Rol\" = 'AdministradorGlobal'"));

        builder.Property(c => c.FechaCreacion)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(c => c.Activo)
            .IsRequired();
    }
}

