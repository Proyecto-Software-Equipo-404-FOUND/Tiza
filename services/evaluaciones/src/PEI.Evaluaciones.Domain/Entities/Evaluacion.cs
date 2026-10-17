using System;

namespace PEI.Evaluaciones.Domain.Entities;

public class Evaluacion
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public string Titulo { get; set; } = string.Empty;                              // nueva
    public string Tipo { get; set; } = string.Empty;
    public EstadoEvaluacion Estado { get; set; } = EstadoEvaluacion.Borrador;       // nueva
    public Guid? CohorteId { get; set; }                                            // nueva
    public string PreguntasJson { get; set; } = string.Empty;
    public DateTimeOffset FechaCreacion { get; set; }                               // nueva
    public DateTimeOffset FechaActualizacion { get; set; }                          // nueva
    public int Version { get; set; } = 1;
}
