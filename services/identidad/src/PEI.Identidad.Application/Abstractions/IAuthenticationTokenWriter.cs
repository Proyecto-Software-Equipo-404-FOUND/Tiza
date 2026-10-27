using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Abstractions;

public interface IAuthenticationTokenWriter
{
    // El callback solo emite el resultado desde la identidad bloqueada. No hace I/O.
    // El resultado se devuelve unicamente despues del commit; cualquier fallo revierte la sesion.
    Task<T> ConfirmarLoginAsync<T>(Usuario autenticado, string tokenHash, DateTime vencimiento,
        Func<RefreshIdentity, T> emitir, CancellationToken cancellationToken = default);
    Task<T> ConfirmarRefreshAsync<T>(string anteriorHash, string nuevoHash, DateTime vencimiento,
        Func<RefreshIdentity, T> emitir, CancellationToken cancellationToken = default);

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
