using PEI.SalaEnVivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PEI.SalaEnVivo.Infrastructure.Persistence
{
    public class pei_sala_db : DbContext
    {
        public pei_sala_db(DbContextOptions<pei_sala_db> options) : base(options) { }

        public DbSet<Sala> Sala => Set<Sala>();
        public DbSet<ParticipanteSala> ParticipanteSala => Set<ParticipanteSala>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Sala>(s =>
            {
                s.HasKey(x => x.Id);
                s.HasIndex(x => x.CodigoPIN);
            });

            modelBuilder.Entity<ParticipanteSala>(p =>
            {
                p.HasOne(x => x.Sala)
                    .WithMany()
                    .HasForeignKey(x => x.SalaId);
            });
        }
    }
}
