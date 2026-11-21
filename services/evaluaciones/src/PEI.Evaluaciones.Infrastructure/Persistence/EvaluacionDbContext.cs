using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

public class EvaluacionDbContext : DbContext
{
    public EvaluacionDbContext(DbContextOptions<EvaluacionDbContext> options) : base(options)
    {
    }

    public DbSet<Evaluacion> Evaluaciones => Set<Evaluacion>();
    public DbSet<EntregaExamen> EntregasExamen => Set<EntregaExamen>();

    public DbSet<CalificacionSugerida> CalificacionesSugeridas => Set<CalificacionSugerida>();
    public DbSet<CalificacionDocente> CalificacionesDocente => Set<CalificacionDocente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de la tabla Evaluacion
        modelBuilder.Entity<Evaluacion>(entity =>
        {
            entity.ToTable("Evaluacion");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstitucionId).IsRequired();
            entity.Property(e => e.PreguntasJson).IsRequired().HasColumnType("jsonb");

            entity.Property(e => e.Titulo).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.FechaCreacion).IsRequired();
            entity.Property(e => e.FechaActualizacion).IsRequired();

            // Clave para la concurrencia optimista (EV-04)
            entity.Property(e => e.Version).IsConcurrencyToken().IsRequired();
        });

        // Configuración de la tabla EntregaExamen
        modelBuilder.Entity<EntregaExamen>(entity =>
        {
            entity.ToTable("EntregaExamen");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstitucionId).IsRequired();
            entity.Property(e => e.EvaluacionId).IsRequired();
            entity.Property(e => e.SalaId).IsRequired();
            entity.Property(e => e.AlumnoId).IsRequired();
            entity.Property(e => e.RespuestasJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.AlertasFocoJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.NotaMultipleChoice).IsRequired(false);
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.FechaEntrega).IsRequired();
            entity.Property(e => e.EstadoNota).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EstadoNotaEntrega.Sugerida).IsRequired();
            entity.Property(e => e.NotaFinal).HasPrecision(6, 2).IsRequired(false);
            entity.Property(e => e.PuntajeMaximo).HasPrecision(6, 2).IsRequired(false);
            entity.Property(e => e.FechaAprobacionUtc).IsRequired(false);
            entity.Property(e => e.DocenteAprobadorId).IsRequired(false);
            entity.HasIndex(e => new { e.SalaId, e.AlumnoId }).IsUnique();

            // S2-EV-02 (#144): concurrencia optimista entre el worker y el callback de IA.
            entity.Property(e => e.Version).IsConcurrencyToken().IsRequired();
            entity.HasOne<Evaluacion>()
                .WithMany()
                .HasForeignKey(e => e.EvaluacionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // S2-EV-02 (#144): nota sugerida por IA, una por (entrega, pregunta).
        modelBuilder.Entity<CalificacionSugerida>(entity =>
        {
            entity.ToTable("CalificacionSugerida");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.InstitucionId).IsRequired();
            entity.Property(c => c.EntregaId).IsRequired();
            entity.Property(c => c.PreguntaId).IsRequired();
            entity.Property(c => c.NotaSugerida).HasPrecision(6, 2).IsRequired();
            entity.Property(c => c.Justificacion).IsRequired().HasMaxLength(4000);
            entity.Property(c => c.DesgloseJson).HasColumnType("jsonb");
            entity.Property(c => c.FechaRegistro).IsRequired();
            entity.HasIndex(c => new { c.EntregaId, c.PreguntaId }).IsUnique();
            entity.HasOne<EntregaExamen>()
                .WithMany()
                .HasForeignKey(c => c.EntregaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // S3-EV-03 (#244): calificación final del docente, una por (entrega, pregunta).
        modelBuilder.Entity<CalificacionDocente>(entity =>
        {
            entity.ToTable("CalificacionDocente");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.InstitucionId).IsRequired();
            entity.Property(c => c.EntregaId).IsRequired();
            entity.Property(c => c.PreguntaId).IsRequired();
            entity.Property(c => c.PuntajeDocente).HasPrecision(6, 2).IsRequired();
            entity.Property(c => c.ComentarioDocente).HasMaxLength(1000);
            entity.Property(c => c.DesgloseJson).HasColumnType("jsonb");
            entity.Property(c => c.FechaEdicionUtc).IsRequired();
            entity.HasIndex(c => new { c.EntregaId, c.PreguntaId }).IsUnique();
            entity.HasOne<EntregaExamen>()
                .WithMany()
                .HasForeignKey(c => c.EntregaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
