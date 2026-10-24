namespace PEI.Identidad.Application.Abstractions;

public interface IAuthenticationTokenWriter
{
    Task<bool> RotarRefreshTokenAsync(string tokenAnteriorHash, string tokenNuevoHash,
        DateTime fechaExpiracion, CancellationToken cancellationToken = default);

    /// <exception cref="PEI.Identidad.Application.Exceptions.AuthenticationIdentityUnavailableException">
    /// El usuario o su institución ya no están disponibles para crear la sesión.
    /// Los errores técnicos de persistencia no se convierten en este resultado.
    /// </exception>
    Task CrearRefreshTokenAsync(
        Guid usuarioId,
        string tokenHash,
        DateTime fechaExpiracion,
        CancellationToken cancellationToken = default);

    Task<bool> RevocarRefreshTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);
}
