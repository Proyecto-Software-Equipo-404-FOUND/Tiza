
using System.Security.Cryptography;
using System.Text;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Infrastructure.Security;

public class RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
