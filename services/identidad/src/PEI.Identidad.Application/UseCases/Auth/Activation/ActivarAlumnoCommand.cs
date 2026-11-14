using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Validation;

namespace PEI.Identidad.Application.UseCases.Auth.Activation;

public sealed class ActivarAlumnoCommand
{
    [Required, MaxLength(100)] public string Token { get; set; } = "";
    [Required, MinLength(8), PasswordBytes] public string Password { get; set; } = "";
}
