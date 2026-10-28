using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Interfaces.Repositories;

//Esta interfaz permite buscar usuarios por email, validar la institución y guardar el registro.

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(string email);

    Task<bool> InstitucionActivaExisteAsync(Guid institucionId);

    Task AddAsync(Usuario usuario);

    Task SaveChangesAsync();
}