
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.UseCases.Auth.Register;

// El registro comprueba el email, valida la institución y el rol,
// genera el hash de la contraseña y guarda al usuario.

public class RegisterHandler
{
    private static readonly string[] RolesPermitidos =
    {
        "Alumno",
        "Docente",
        "Administrador"
    };

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> HandleAsync(RegisterCommand command)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        var usuarioExistente =
            await _usuarioRepository.GetByEmailAsync(email);

        if (usuarioExistente is not null)
        {
            throw new InvalidOperationException(
                "Ya existe un usuario con ese email.");
        }

        var institucionActiva =
            await _usuarioRepository.InstitucionActivaExisteAsync(
                command.InstitucionId);

        if (!institucionActiva)
        {
            throw new InvalidOperationException(
                "La institución no existe o está inactiva.");
        }

        var rol = command.Rol.Trim();

        if (!RolesPermitidos.Contains(
                rol,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El rol indicado no es válido. Los roles permitidos son: Alumno, Docente y Administrador.");
        }

        // Normaliza el nombre del rol para mantener valores consistentes.
        rol = RolesPermitidos.First(
            r => r.Equals(rol, StringComparison.OrdinalIgnoreCase));

        var usuario = new Usuario
        {
            InstitucionId = command.InstitucionId,
            Email = email,
            PasswordHash = _passwordHasher.Hash(command.Password),
            Nombre = command.Nombre.Trim(),
            Apellido = command.Apellido.Trim(),
            Rol = rol,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        await _usuarioRepository.AddAsync(usuario);
        await _usuarioRepository.SaveChangesAsync();

        return usuario.Id;
    }
}
