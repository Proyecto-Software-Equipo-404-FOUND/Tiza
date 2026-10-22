
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.UnitTests;

public class AuthHandlerTests
{
    private static readonly Guid InstitucionId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static RegisterCommand CrearRegistro() => new()
    {
        Rol = "Docente",
        Email = " Prueba@Example.com ",
        Password = "Prueba123!",
        Nombre = " Usuario ",
        Apellido = " Prueba "
    };

    private static Usuario CrearUsuario(
        bool usuarioActivo = true,
        bool institucionActiva = true) => new()
        {
            Id = Guid.NewGuid(),
            InstitucionId = InstitucionId,
            Email = "prueba@example.com",
            PasswordHash = "HASH:Prueba123!",
            Nombre = "Usuario",
            Apellido = "Prueba",
            Rol = "Docente",
            Activo = usuarioActivo,
            Institucion = new Institucion
            {
                Id = InstitucionId,
                Nombre = "Institucion de prueba",
                Codigo = "INST-001",
                Activo = institucionActiva
            }
        };

    [Fact]
    public async Task RegistroValido_GuardaUsuarioYDevuelveId()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        var handler = new RegisterHandler(
            repositorio, new HasherFalso(), new TenantPrueba());

        var id = await handler.HandleAsync(CrearRegistro());

        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(repositorio.Usuarios);
        Assert.Equal(id, repositorio.Usuarios[0].Id);
        Assert.Equal("Prueba@Example.com", repositorio.Usuarios[0].Email);
        Assert.Equal("Usuario", repositorio.Usuarios[0].Nombre);
        Assert.Equal("Prueba", repositorio.Usuarios[0].Apellido);
        Assert.Equal("Docente", repositorio.Usuarios[0].Rol);
        Assert.True(repositorio.Guardado);
    }

    [Fact]
    public async Task RegistroConEmailDuplicado_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var handler = new RegisterHandler(
            repositorio, new HasherFalso(), new TenantPrueba());

        var error = await Assert.ThrowsAsync<RegistrationConflictException>(
            () => handler.HandleAsync(CrearRegistro()));

        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public async Task RegistroConInstitucionInactiva_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(
            InstitucionId, institucionActiva: false);

        var handler = new RegisterHandler(
            repositorio, new HasherFalso(), new TenantPrueba());

        var error = await Assert.ThrowsAsync<TenantAccessDeniedException>(
            () => handler.HandleAsync(CrearRegistro()));

        Assert.NotEmpty(error.Message);

        Assert.Empty(repositorio.Usuarios);
    }

    [Fact]
    public async Task LoginValido_DevuelveToken()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso());

        var token = await handler.HandleAsync(new LoginCommand
        {
            Email = " Prueba@Example.com ",
            Password = "Prueba123!"
        });

        Assert.Equal("token-de-prueba", token);
    }

    [Fact]
    public async Task LoginConPasswordIncorrecta_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(CrearUsuario());

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Incorrecta123!"
            }));
    }

    [Fact]
    public async Task LoginConUsuarioInexistente_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "inexistente@example.com",
                Password = "Prueba123!"
            }));
    }

    [Fact]
    public async Task LoginConUsuarioInactivo_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(
            CrearUsuario(usuarioActivo: false));

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Prueba123!"
            }));
    }

    [Fact]
    public async Task LoginConInstitucionInactiva_LanzaExcepcion()
    {
        var repositorio = new RepositorioFalso(InstitucionId);
        repositorio.AgregarExistente(
            CrearUsuario(institucionActiva: false));

        var handler = new LoginHandler(
            repositorio,
            new HasherFalso(),
            new TokenFalso());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(new LoginCommand
            {
                Email = "prueba@example.com",
                Password = "Prueba123!"
            }));
    }

    private sealed class TenantPrueba : ITenantContext { public Guid? InstitucionId => AuthHandlerTests.InstitucionId; }

    private sealed class RepositorioFalso : IRegistrationDataAccess, IAuthenticationDataAccess
    {
        private readonly List<Usuario> _usuarios = new();
        private readonly Guid _institucionId;
        private readonly bool _institucionActiva;

        public RepositorioFalso(
            Guid institucionId,
            bool institucionActiva = true)
        {
            _institucionId = institucionId;
            _institucionActiva = institucionActiva;
        }

        public IReadOnlyList<Usuario> Usuarios => _usuarios;
        public bool Guardado { get; private set; }

        public void AgregarExistente(Usuario usuario)
        {
            _usuarios.Add(usuario);
        }

        public Task<Usuario?> GetByEmailAsync(string email)
        {
            Usuario? usuario = _usuarios.FirstOrDefault(
                u => u.Email == email);

            return Task.FromResult(usuario);
        }

        public Task<bool> PuedeRegistrarAsync(
            Guid institucionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                institucionId == _institucionId &&
                _institucionActiva);
        }

        public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(_usuarios.Any(u => string.Equals(u.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<Usuario?> BuscarUsuarioParaLoginAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usuarios.FirstOrDefault(u => u.Activo && u.Institucion.Activo && string.Equals(u.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<RefreshToken?> BuscarRefreshTokenAsync(string hash, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
        public Task<RefreshIdentity?> BuscarIdentidadParaRenovacionAsync(string hash, CancellationToken cancellationToken = default) => Task.FromResult<RefreshIdentity?>(null);

        public Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
        {
            _usuarios.Add(usuario);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Guardado = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public void VerifyDummy(string password) { }
        public string Hash(string password)
        {
            return "HASH:" + password;
        }

        public bool Verify(string password, string hashedPassword)
        {
            return Hash(password) == hashedPassword;
        }
    }

    private sealed class TokenFalso : ITokenService
    {
        public string GenerateToken(Usuario usuario)
        {
            return "token-de-prueba";
        }
    }
}
