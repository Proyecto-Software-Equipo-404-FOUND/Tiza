using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.UseCases.Auth.Register;

//El registro comprueba el email, valida la institución, genera el hash y guarda al usuario. El rol inicial queda fijado en "Usuario".

public class RegisterHandler
{
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

        var usuarioExistente = await _usuarioRepository.GetByEmailAsync(email);

        if (usuarioExistente is not null)
            throw new InvalidOperationException("Ya existe un usuario con ese email.");

        var institucionActiva = await _usuarioRepository
            .InstitucionActivaExisteAsync(command.InstitucionId);

        if (!institucionActiva)
            throw new InvalidOperationException("La institución no existe o está inactiva.");

        var usuario = new Usuario
        {
            InstitucionId = command.InstitucionId,
            Email = email,
            PasswordHash = _passwordHasher.Hash(command.Password),
            Nombre = command.Nombre.Trim(),
            Apellido = command.Apellido.Trim(),
            Rol = "Usuario",
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        await _usuarioRepository.AddAsync(usuario);
        await _usuarioRepository.SaveChangesAsync();

        return usuario.Id;
    }
}