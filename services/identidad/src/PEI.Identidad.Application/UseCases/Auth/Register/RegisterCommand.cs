using System.ComponentModel.DataAnnotations;

namespace PEI.Identidad.Application.UseCases.Auth.Register;

public class RegisterCommand
{
    [Required(ErrorMessage = "El rol es obligatorio")]
    public string Rol { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La institución es obligatoria")]
    public Guid InstitucionId { get; set; }

    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del email no es válido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio")]
    public string Apellido { get; set; } = string.Empty;
}