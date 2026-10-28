using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IdentidadDbContext _context;

    public UsuarioRepository(IdentidadDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        return await _context.Usuarios
            .Include(u => u.Institucion)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> InstitucionActivaExisteAsync(Guid institucionId)
    {
        return await _context.Instituciones
            .AnyAsync(i => i.Id == institucionId && i.Activo);
    }

    public async Task AddAsync(Usuario usuario)
    {
        await _context.Usuarios.AddAsync(usuario);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}