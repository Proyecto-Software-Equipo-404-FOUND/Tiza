using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Interfaces.Service;

//define cómo solicitar un JWT

public interface ITokenService
{
    string GenerateToken(Usuario usuario);
}