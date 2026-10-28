using PEI.SalaEnVivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PEI.SalaEnVivo.Infrastructure.Persistence
{
    public class pei_sala_db : DbContext
    {
        public pei_sala_db(DbContextOptions<pei_sala_db> options) : base(options) { }

        public DbSet<Sala> Sala => Set<Sala>();
        public DbSet<ParticipanteSala> ParticipanteSala => Set<ParticipanteSala>();
        public DbSet<RespuestaSala> RespuestasSala => Set<RespuestaSala>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Sala>(s =>
            {
                s.HasKey(x => x.Id);
                s.HasIndex(x => x.CodigoPIN).IsUnique();
            });

            modelBuilder.Entity<ParticipanteSala>(p =>
            {
                p.HasOne(x => x.Sala)
                    .WithMany()
                    .HasForeignKey(x => x.SalaId);
            });

            modelBuilder.Entity<RespuestaSala>(r =>
            {
                r.ToTable("RespuestasSala");
                r.HasKey(x => x.Id);

                r.Property(x => x.Respuesta)
                    .IsRequired()
                    .HasMaxLength(10000);

                // 1. Una sola respuesta por pregunta y alumno
                r.HasIndex(x => new { x.SalaId, x.AlumnoId, x.PreguntaId })
                    .IsUnique()
                    .HasDatabaseName("ux_respuesta_sala_pregunta");

                // 2. Una sola clave de idempotencia por alumno y sala
                r.HasIndex(x => new { x.SalaId, x.AlumnoId, x.IdempotencyKey })
                    .IsUnique()
                    .HasDatabaseName("ux_respuesta_sala_idempotency");

                // 3. Secuencia incremental única por alumno y sala
                r.HasIndex(x => new { x.SalaId, x.AlumnoId, x.Secuencia })
                    .IsUnique()
                    .HasDatabaseName("ux_respuesta_sala_secuencia");

                r.HasOne(x => x.Sala)
                    .WithMany()
                    .HasForeignKey(x => x.SalaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
