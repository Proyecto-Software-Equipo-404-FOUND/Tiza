using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.UseCases.Auth.Login;

namespace PEI.Identidad.Application.Plataforma;

public sealed class PlataformaHandlers(IPlataformaDataAccess data, IPasswordHasher passwords, IPlataformaTokenService tokens)
{
    public async Task<string> LoginAsync(LoginCommand command, CancellationToken ct)
    {
        Validator.ValidateObject(command, new ValidationContext(command), true);
        var cuenta = await data.BuscarLoginAsync(command.Email, ct);
        if (cuenta is null) { passwords.VerifyDummy(command.Password); throw new UnauthorizedAccessException(); }
        if (!passwords.Verify(command.Password, cuenta.PasswordHash) || !cuenta.Activo || cuenta.Rol != "AdministradorGlobal")
            throw new UnauthorizedAccessException();
        return tokens.Emitir(cuenta);
    }

    public Task<InstitucionCreada> CrearAsync(Guid actor, CrearInstitucionCommand command, CancellationToken ct)
    {
        Validator.ValidateObject(command, new ValidationContext(command), true);
        return data.CrearInstitucionAsync(actor, command, ct);
    }

    public Task ActivarAsync(ActivarAdministradorCommand command, CancellationToken ct)
    {
        Validator.ValidateObject(command, new ValidationContext(command), true);
        return data.ActivarAsync(command.Token, passwords.Hash(command.Password), ct);
    }
}
