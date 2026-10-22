using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Validation;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Plataforma;

public sealed class CrearInstitucionCommand : IValidatableObject
{
    [Required, MaxLength(150)] public string Nombre { get; set; } = "";
    [Required, MaxLength(30)] public string Codigo { get; set; } = "";
    [Required, EmailAddress, MaxLength(150)] public string EmailAdministrador { get; set; } = "";
    [Required, MaxLength(100)] public string NombreAdministrador { get; set; } = "";
    [Required, MaxLength(100)] public string ApellidoAdministrador { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (new[] { Nombre, Codigo, NombreAdministrador, ApellidoAdministrador }.Any(string.IsNullOrWhiteSpace))
            yield return new ValidationResult("Los nombres y el código son obligatorios.");
    }
}

public sealed class ActivarAdministradorCommand
{
    [Required, MaxLength(100)] public string Token { get; set; } = "";
    [Required, MinLength(8), PasswordBytes] public string Password { get; set; } = "";
}

public sealed record InstitucionResumen(Guid Id, string Nombre, string Codigo, bool Activo);
public sealed record InstitucionCreada(Guid InstitucionId, Guid AdministradorId);
public sealed record MensajeEmail(Guid Id, string Destinatario, string Contenido);

public interface IEmailSender
{
    Task EnviarAsync(MensajeEmail mensaje, CancellationToken cancellationToken);
}

public interface IPlataformaDataAccess
{
    Task<CuentaPlataforma?> BuscarLoginAsync(string email, CancellationToken ct);
    Task<bool> EstaActivaAsync(Guid id, CancellationToken ct);
    Task<InstitucionCreada> CrearInstitucionAsync(Guid actor, CrearInstitucionCommand command, CancellationToken ct);
    Task<IReadOnlyList<InstitucionResumen>> ListarInstitucionesAsync(Guid actor, int pagina, CancellationToken ct);
    Task ActivarAsync(string token, string passwordHash, CancellationToken ct);
}

public interface IPlataformaTokenService { string Emitir(CuentaPlataforma cuenta); }
