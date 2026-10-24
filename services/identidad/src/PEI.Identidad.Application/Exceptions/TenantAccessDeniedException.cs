namespace PEI.Identidad.Application.Exceptions;

public sealed class TenantAccessDeniedException : Exception
{
    public TenantAccessDeniedException()
        : base("La operacion no esta autorizada para la institucion actual.")
    {
    }
}
