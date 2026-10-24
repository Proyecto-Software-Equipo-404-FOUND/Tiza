
namespace PEI.Identidad.Application.Interfaces.Security;

public interface IRefreshTokenHasher
{
    string Hash(string token);
}
