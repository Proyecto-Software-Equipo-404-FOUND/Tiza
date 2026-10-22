using System.Text;
using PEI.Identidad.Application.Interfaces.Security;
namespace PEI.Identidad.Infrastructure.Security;
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-not-an-account-password", workFactor: 12);
    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("La contrasena debe tener al menos 8 caracteres y como maximo 72 bytes UTF-8.");
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }
    public bool Verify(string password, string hashedPassword)
    {
        if (password is null || Encoding.UTF8.GetByteCount(password) > 72) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, hashedPassword); }
        catch (Exception e) when (e is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        { VerifyDummy(password); return false; }
    }
    public void VerifyDummy(string password)
    {
        if (password is not null && Encoding.UTF8.GetByteCount(password) <= 72)
            _ = BCrypt.Net.BCrypt.Verify(password, DummyHash);
    }
}
