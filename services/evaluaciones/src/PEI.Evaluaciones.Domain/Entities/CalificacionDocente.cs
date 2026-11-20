namespace PEI.Evaluaciones.Domain.Entities;

public class CalificacionDocente
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid EntregaId { get; set; }
    public Guid PreguntaId { get; set; }
    public decimal PuntajeDocente { get; set; }
    public string? ComentarioDocente { get; set; }
    public string? DesgloseJson { get; set; }
    public DateTimeOffset FechaEdicionUtc { get; set; }
}