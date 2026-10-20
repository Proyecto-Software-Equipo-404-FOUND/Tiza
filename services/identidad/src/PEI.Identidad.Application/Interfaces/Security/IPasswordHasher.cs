namespace PEI.Identidad.Application.Interfaces.Security;
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);
    void VerifyDummy(string password);
}
