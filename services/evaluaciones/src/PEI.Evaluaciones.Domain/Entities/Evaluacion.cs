using System;

namespace PEI.Evaluaciones.Domain.Entities;

public class Evaluacion
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public string Titulo { get; set; } = string.Empty;                              
    public string Tipo { get; set; } = string.Empty;
    public EstadoEvaluacion Estado { get; set; } = EstadoEvaluacion.Borrador;       
    public Guid? CohorteId { get; set; }                                            
    public string PreguntasJson { get; set; } = string.Empty;
    public DateTimeOffset FechaCreacion { get; set; }                               
    public DateTimeOffset FechaActualizacion { get; set; }                          
    public int Version { get; set; } = 1;
}
