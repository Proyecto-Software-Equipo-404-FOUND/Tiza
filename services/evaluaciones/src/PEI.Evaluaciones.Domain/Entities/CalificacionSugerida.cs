namespace PEI.Evaluaciones.Domain.Entities;

// S2-EV-02 (#144): nota que la IA SUGIERE para una pregunta abierta de una entrega.
// Nunca es la nota final: la aprueba el docente (Sprint 3, RN-COR-07).
// Hay como máximo una por (EntregaId, PreguntaId).
public class CalificacionSugerida
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid EntregaId { get; set; }
    public Guid PreguntaId { get; set; }
    public decimal NotaSugerida { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public string? DesgloseJson { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
}
