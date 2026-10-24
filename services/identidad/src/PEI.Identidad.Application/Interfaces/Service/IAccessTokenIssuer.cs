using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Interfaces.Service;

// Metadata del mismo JWT emitido: evita inventar vencimientos en los casos de uso.
public sealed record IssuedAccessToken(string AccessToken, DateTime ExpiresAt);
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Usuario usuario);
}
