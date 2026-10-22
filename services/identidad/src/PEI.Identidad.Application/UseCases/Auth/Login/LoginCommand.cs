using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Validation;
namespace PEI.Identidad.Application.UseCases.Auth.Login;
public sealed class LoginCommand
{
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, PasswordBytes] public string Password { get; set; } = string.Empty;
}
