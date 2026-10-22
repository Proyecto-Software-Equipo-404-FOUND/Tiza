namespace PEI.Identidad.Application.Exceptions;
public sealed class RegistrationConflictException : Exception
{
    public RegistrationConflictException() : base("No se pudo completar el registro: los datos entran en conflicto.") { }
}
