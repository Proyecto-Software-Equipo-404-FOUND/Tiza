using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.UseCases.Auth.Activation;

namespace PEI.Identidad.UnitTests;

public sealed class ActivarAlumnoHandlerTests
{
    [Fact]
    public async Task ValidaPasswordYEntregaHashAlPuertoDePersistencia()
    {
        var data = new FakeActivationData();
        var hasher = new FakeHasher();
        var handler = new ActivarAlumnoHandler(data, hasher);

        await handler.HandleAsync(new ActivarAlumnoCommand { Token = "token", Password = "NuevaClave123!" }, default);

        Assert.Equal("token", data.Token);
        Assert.Equal("hash:NuevaClave123!", data.PasswordHash);
        Assert.Equal(1, data.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    [InlineData("clave-de-mas-de-72-bytes-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task RechazaPasswordFueraDePolitica(string password)
    {
        var data = new FakeActivationData();
        var handler = new ActivarAlumnoHandler(data, new FakeHasher());

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            handler.HandleAsync(new ActivarAlumnoCommand { Token = "token", Password = password }, default));
        Assert.Equal(0, data.CallCount);
    }

    private sealed class FakeActivationData : IActivacionAlumnoDataAccess
    {
        public string? Token { get; private set; }
        public string? PasswordHash { get; private set; }
        public int CallCount { get; private set; }
        public Task ActivarAsync(string token, string passwordHash, CancellationToken ct)
        {
            Token = token;
            PasswordHash = passwordHash;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hashedPassword) => false;
        public void VerifyDummy(string password) { }
    }
}
