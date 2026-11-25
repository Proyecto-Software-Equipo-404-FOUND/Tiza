using System.Text;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Application.UseCases.Auth.CambiarContrasena;

public sealed record CambiarContrasenaCommand(Guid UsuarioId, string? ContrasenaActual, string? ContrasenaNueva);

public enum CambiarContrasenaResultado
{
    Exitoso,
    NoEncontrado,
    ContrasenaActualIncorrecta,
    ContrasenaNuevaInvalida,
    ContrasenaIgualALaActual
}

/// <summary>
/// S3-ID-03: el usuario cambia su propia contraseña. Pide la actual, valida la nueva con las mismas reglas
/// del registro (8 caracteres o más, 72 bytes o menos, no solo espacios) y cierra las demás sesiones
/// (revoca los refresh tokens): quien tenga la contraseña vieja deja de poder renovar.
/// </summary>
public sealed class CambiarContrasenaHandler(IUsuariosModificacionDataAccess data, IPasswordHasher hasher)
{
    public const int Minima = 8;
    public const int MaximaBytes = 72;

    public static bool EsValida(string? contrasena) =>
        !string.IsNullOrWhiteSpace(contrasena) &&
        contrasena.Length >= Minima &&
        Encoding.UTF8.GetByteCount(contrasena) <= MaximaBytes;

    public async Task<CambiarContrasenaResultado> HandleAsync(CambiarContrasenaCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var usuario = command.UsuarioId == Guid.Empty
            ? null
            : await data.ObtenerUsuarioAsync(command.UsuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo)
            return CambiarContrasenaResultado.NoEncontrado;

        if (string.IsNullOrEmpty(command.ContrasenaActual) || !hasher.Verify(command.ContrasenaActual, usuario.PasswordHash))
            return CambiarContrasenaResultado.ContrasenaActualIncorrecta;

        if (!EsValida(command.ContrasenaNueva))
            return CambiarContrasenaResultado.ContrasenaNuevaInvalida;

        if (command.ContrasenaNueva == command.ContrasenaActual)
            return CambiarContrasenaResultado.ContrasenaIgualALaActual;

        await data.CambiarContrasenaAsync(usuario.Id, hasher.Hash(command.ContrasenaNueva!), cancellationToken);
        return CambiarContrasenaResultado.Exitoso;
    }
}
