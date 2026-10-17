using System;

namespace PEI.Evaluaciones.Domain.Entities;

public class Evaluacion
{
    public Guid Id { get; set; }
    public Guid InstitucionId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string PreguntasJson { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
}
