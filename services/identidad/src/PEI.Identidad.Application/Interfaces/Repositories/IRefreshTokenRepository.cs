
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);

    Task AddAsync(RefreshToken refreshToken);

    Task SaveChangesAsync();
}
