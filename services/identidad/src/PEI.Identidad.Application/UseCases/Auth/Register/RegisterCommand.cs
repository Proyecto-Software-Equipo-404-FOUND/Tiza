using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Validation;
using PEI.Identidad.Domain.Security;
namespace PEI.Identidad.Application.UseCases.Auth.Register;
public sealed class RegisterCommand
{
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), PasswordBytes] public string Password { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Nombre { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Apellido { get; set; } = string.Empty;
    [Required, AllowedValues(IdentityRoles.Docente, IdentityRoles.Alumno)] public string Rol { get; set; } = string.Empty;
}
