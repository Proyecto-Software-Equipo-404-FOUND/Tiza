
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentidadDbContext _context;

    public RefreshTokenRepository(IdentidadDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .Include(t => t.Usuario)
            .ThenInclude(u => u.Institucion)
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
