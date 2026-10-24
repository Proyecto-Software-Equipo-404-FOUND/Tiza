using PEI.Identidad.Domain.Entities;
namespace PEI.Identidad.Application.Abstractions;
public interface IRegistrationDataAccess
{
    Task<bool> PuedeRegistrarAsync(Guid institucionId);
    Task<bool> ExisteEmailAsync(string email);
    Task AddAsync(Usuario usuario);
    Task SaveChangesAsync();
}
public interface IAuthenticatedUserValidator
{
    Task<bool> IsActiveAsync(Guid usuarioId, Guid institucionId, string role, CancellationToken cancellationToken);
}
