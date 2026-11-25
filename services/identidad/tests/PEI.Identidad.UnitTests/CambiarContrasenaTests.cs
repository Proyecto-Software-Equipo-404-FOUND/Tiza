using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.UseCases.Auth.CambiarContrasena;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.UnitTests;

// S3-ID-03: cambio de contraseña del propio usuario
public class CambiarContrasenaTests
{
    private sealed class HasherFalso : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hashedPassword) => hashedPassword == "hash:" + password;
        public void VerifyDummy(string password) { }
    }

    private sealed class DatosFalsos : IUsuariosModificacionDataAccess
    {
        public Dictionary<Guid, Usuario> Usuarios { get; } = new();
        public string? HashGuardado { get; private set; }

        public Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Usuarios.GetValueOrDefault(id));

        public Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CambiarContrasenaAsync(Guid usuarioId, string nuevoHash, CancellationToken cancellationToken = default)
        {
            HashGuardado = nuevoHash;
            Usuarios[usuarioId].PasswordHash = nuevoHash;
            return Task.CompletedTask;
        }
    }

    private static (DatosFalsos Datos, Usuario Usuario, CambiarContrasenaHandler Handler) Preparar(bool activo = true)
    {
        var datos = new DatosFalsos();
        var usuario = new Usuario { Id = Guid.NewGuid(), Rol = IdentityRoles.Alumno, Activo = activo, PasswordHash = "hash:Clave1234" };
        datos.Usuarios[usuario.Id] = usuario;
        return (datos, usuario, new CambiarContrasenaHandler(datos, new HasherFalso()));
    }

    [Fact]
    public async Task ConLaActualCorrectaYUnaNuevaValida_GuardaLaNueva()
    {
        var (datos, usuario, handler) = Preparar();

        var resultado = await handler.HandleAsync(new CambiarContrasenaCommand(usuario.Id, "Clave1234", "OtraClave99"));

        Assert.Equal(CambiarContrasenaResultado.Exitoso, resultado);
        Assert.Equal("hash:OtraClave99", datos.HashGuardado);
    }

    [Fact]
    public async Task ConLaActualIncorrecta_NoCambiaNada()
    {
        var (datos, usuario, handler) = Preparar();

        var resultado = await handler.HandleAsync(new CambiarContrasenaCommand(usuario.Id, "equivocada", "OtraClave99"));

        Assert.Equal(CambiarContrasenaResultado.ContrasenaActualIncorrecta, resultado);
        Assert.Null(datos.HashGuardado);
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("        ")]
    [InlineData("")]
    public async Task ConUnaNuevaInvalida_NoCambiaNada(string nueva)
    {
        var (datos, usuario, handler) = Preparar();

        var resultado = await handler.HandleAsync(new CambiarContrasenaCommand(usuario.Id, "Clave1234", nueva));

        Assert.Equal(CambiarContrasenaResultado.ContrasenaNuevaInvalida, resultado);
        Assert.Null(datos.HashGuardado);
    }

    [Fact]
    public async Task ConLaMismaContrasena_LaRechaza()
    {
        var (datos, usuario, handler) = Preparar();

        var resultado = await handler.HandleAsync(new CambiarContrasenaCommand(usuario.Id, "Clave1234", "Clave1234"));

        Assert.Equal(CambiarContrasenaResultado.ContrasenaIgualALaActual, resultado);
        Assert.Null(datos.HashGuardado);
    }

    [Fact]
    public async Task UsuarioDadoDeBaja_NoPuedeCambiarla()
    {
        var (datos, usuario, handler) = Preparar(activo: false);

        var resultado = await handler.HandleAsync(new CambiarContrasenaCommand(usuario.Id, "Clave1234", "OtraClave99"));

        Assert.Equal(CambiarContrasenaResultado.NoEncontrado, resultado);
        Assert.Null(datos.HashGuardado);
    }
}
