using services.sala-en-vivo.src.PEI.SalaEnVivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace services.sala-en-vivo.src.PEI.SalaEnVivo.Infrastructure.Persistence;
public class pei_sala_db : DbContext
{
    public pei_sala_db(DbContextOptions<pei_sala_db> options) : base(options) { }

    public DbSet<Sala> Sala => Set<Sala>();
    public DbSet<ParticipanteSala> ParticipanteSala => Set<ParticipanteSala>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Sala>();

        modelBuilder.Entity<ParticipanteSala>(p =>
        {
            p.HasOne(s => s.Sala)
            .WithMany()
            .HasForeignKey(s => s.SalaId);
        });
    }
}
