using System.Security.Cryptography;
using PEI.Identidad.Application.Interfaces.Service;

namespace PEI.Identidad.Infrastructure.Security;

public class RefreshTokenService : IRefreshTokenService
{
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}