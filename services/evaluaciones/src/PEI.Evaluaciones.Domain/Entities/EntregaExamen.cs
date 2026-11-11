using System;

namespace PEI.Evaluaciones.Domain.Entities;

public class EntregaExamen
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public Guid EvaluacionId { get; set; }
    public Guid SalaId { get; set; }
    public Guid AlumnoId { get; set; }
    public string RespuestasJson { get; set; } = string.Empty;
    public string AlertasFocoJson { get; set; } = string.Empty;
    public decimal? NotaMultipleChoice { get; set; }
    public EstadoEntregaExamen Estado { get; set; }
    public DateTimeOffset FechaEntrega { get; set; }

    // S2-EV-02 (#144): concurrencia optimista. El worker y el callback de IA pueden
    // modificar la misma entrega al mismo tiempo.
    public int Version { get; set; } = 1;
}