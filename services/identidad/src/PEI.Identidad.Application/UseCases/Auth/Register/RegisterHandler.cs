using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
namespace PEI.Identidad.Application.UseCases.Auth.Register;
public sealed class RegisterHandler(IRegistrationDataAccess data, IPasswordHasher hasher, ITenantContext tenant)
{
    public async Task<Guid> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenant.InstitucionId is not Guid institucion || institucion == Guid.Empty || !await data.PuedeRegistrarAsync(institucion, cancellationToken))
            throw new TenantAccessDeniedException();
        Validator.ValidateObject(command, new ValidationContext(command), true);
        if (!IdentityRoles.IsAssignable(command.Rol)) throw new ValidationException("Rol no permitido.");
        if (await data.ExisteEmailAsync(command.Email, cancellationToken)) throw new RegistrationConflictException();
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = new Usuario
        {
            InstitucionId = institucion,
            Email = command.Email,
            PasswordHash = hasher.Hash(command.Password),
            Nombre = command.Nombre.Trim(),
            Apellido = command.Apellido.Trim(),
            Rol = command.Rol
        };
        // BCrypt es sincronico: observar cancelacion antes de llegar a persistencia.
        cancellationToken.ThrowIfCancellationRequested();
        await data.AddAsync(usuario, cancellationToken);
        await data.SaveChangesAsync(cancellationToken);
        return usuario.Id;
    }
}
