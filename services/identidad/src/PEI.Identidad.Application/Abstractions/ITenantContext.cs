namespace PEI.Identidad.Application.Abstractions;

public interface ITenantContext
{
    Guid? InstitucionId { get; }
}
