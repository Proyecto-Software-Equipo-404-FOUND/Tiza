using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Persistence.PlatformConfigurations;

public sealed class InvitacionConfiguration : IEntityTypeConfiguration<InvitacionAdministrador>
{
    public void Configure(EntityTypeBuilder<InvitacionAdministrador> b)
    {
        b.ToTable("InvitacionAdministrador"); b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => x.UsuarioId).IsUnique();
    }
}

public sealed class EntregaConfiguration : IEntityTypeConfiguration<EntregaInvitacion>
{
    public void Configure(EntityTypeBuilder<EntregaInvitacion> b)
    {
        b.ToTable("EntregaInvitacion"); b.HasKey(x => x.Id);
        b.Property(x => x.Destinatario).HasMaxLength(150).IsRequired();
        b.Property(x => x.ContenidoProtegido).IsRequired();
        b.HasOne<InvitacionAdministrador>().WithMany().HasForeignKey(x => x.InvitacionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InvitacionId).IsUnique();
    }
}
