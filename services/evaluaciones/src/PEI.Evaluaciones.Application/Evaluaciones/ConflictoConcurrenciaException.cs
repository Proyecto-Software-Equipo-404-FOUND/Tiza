namespace PEI.Evaluaciones.Application.Evaluaciones;

// Permite traducir un conflicto de persistencia sin acoplar Application a EF Core.
public sealed class ConflictoConcurrenciaException : Exception
{
    public ConflictoConcurrenciaException(Exception innerException)
        : base("La evaluacion fue modificada por otra persona.", innerException) { }
}
