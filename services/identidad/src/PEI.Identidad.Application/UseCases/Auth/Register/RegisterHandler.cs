using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
namespace PEI.Identidad.Application.UseCases.Auth.Register;
public sealed class RegisterHandler(IRegistrationDataAccess data, IPasswordHasher hasher, ITenantContext tenant)
{
    public async Task<Guid> HandleAsync(RegisterCommand command)
    {
        if (tenant.InstitucionId is not Guid institucion || institucion == Guid.Empty || !await data.PuedeRegistrarAsync(institucion))
            throw new TenantAccessDeniedException();
        Validator.ValidateObject(command, new ValidationContext(command), true);
        if (!IdentityRoles.IsAssignable(command.Rol)) throw new ValidationException("Rol no permitido.");
        if (await data.ExisteEmailAsync(command.Email)) throw new RegistrationConflictException();
        var usuario = new Usuario
        {
            InstitucionId = institucion,
            Email = command.Email,
            PasswordHash = hasher.Hash(command.Password),
            Nombre = command.Nombre.Trim(),
            Apellido = command.Apellido.Trim(),
            Rol = command.Rol
        };
        await data.AddAsync(usuario);
        await data.SaveChangesAsync();
        return usuario.Id;
    }
}
