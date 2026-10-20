namespace PEI.Identidad.Application.Abstractions;

public interface IAuthenticationTokenWriter
{
    Task<bool> RotarRefreshTokenAsync(string tokenAnteriorHash, string tokenNuevoHash,
        DateTime fechaExpiracion, CancellationToken cancellationToken = default);

    Task CrearRefreshTokenAsync(
        Guid usuarioId,
        string tokenHash,
        DateTime fechaExpiracion,
        CancellationToken cancellationToken = default);

    Task<bool> RevocarRefreshTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);
}
