using PEI.Identidad.Domain.Entities;
namespace PEI.Identidad.Application.Abstractions;
public interface IRegistrationDataAccess
{
    Task<bool> PuedeRegistrarAsync(Guid institucionId, CancellationToken cancellationToken = default);
    Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
public interface IAuthenticatedUserValidator
{
    Task<bool> IsActiveAsync(Guid usuarioId, Guid institucionId, string role, CancellationToken cancellationToken);
}
