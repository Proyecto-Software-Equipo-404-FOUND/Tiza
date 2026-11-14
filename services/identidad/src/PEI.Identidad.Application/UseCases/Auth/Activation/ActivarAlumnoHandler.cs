using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Application.UseCases.Auth.Activation;

public sealed class ActivarAlumnoHandler(IActivacionAlumnoDataAccess data, IPasswordHasher passwords)
{
    public Task HandleAsync(ActivarAlumnoCommand command, CancellationToken ct)
    {
        Validator.ValidateObject(command, new ValidationContext(command), validateAllProperties: true);
        return data.ActivarAsync(command.Token, passwords.Hash(command.Password), ct);
    }
}
