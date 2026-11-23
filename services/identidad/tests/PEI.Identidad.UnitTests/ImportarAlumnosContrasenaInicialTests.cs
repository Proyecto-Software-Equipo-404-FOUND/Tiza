using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.UseCases.Usuarios.Importacion;
using Xunit;

namespace PEI.Identidad.UnitTests;

// S2-ID-02 (#135): contraseña inicial definida por el administrador al importar alumnos.
public sealed class ImportarAlumnosContrasenaInicialTests
{
    private sealed class DataAccess : IImportacionAlumnosDataAccess
    {
        public List<string> Pendientes { get; } = [];
        public List<(string Email, string Hash)> Activos { get; } = [];

        public Task<string> NormalizarEmailAsync(string email, CancellationToken ct) => Task.FromResult(email.ToLowerInvariant());

        public Task<ResultadoPersistenciaAlumno> CrearPendienteAsync(Guid admin, Guid institucion, string nombre,
            string apellido, string email, CancellationToken ct)
        {
            Pendientes.Add(email);
            return Task.FromResult(new ResultadoPersistenciaAlumno(email.ToLowerInvariant(), ResultadoAltaAlumno.Creado));
        }

        public Task<ResultadoPersistenciaAlumno> CrearActivoAsync(Guid admin, Guid institucion, string nombre,
            string apellido, string email, string passwordHash, CancellationToken ct)
        {
            Activos.Add((email, passwordHash));
            return Task.FromResult(new ResultadoPersistenciaAlumno(email.ToLowerInvariant(), ResultadoAltaAlumno.Creado));
        }
    }

    private sealed class HasherContador : IPasswordHasher
    {
        public int Llamadas { get; private set; }
        public string Hash(string password) { Llamadas++; return "hash:" + password; }
        public bool Verify(string password, string hashedPassword) => hashedPassword == "hash:" + password;
        public void VerifyDummy(string password) { }
    }

    private static readonly FilaAlumnoCsv[] Filas =
    [
        new(2, "Ana", "Pérez", "ana@example.test"),
        new(3, "Juan", "Gómez", "juan@example.test")
    ];

    [Fact]
    public async Task ConContrasenaInicial_CreaActivosConElMismoHashYSinInvitacion()
    {
        var data = new DataAccess();
        var hasher = new HasherContador();

        var resultado = await new ImportarAlumnosHandler(data, hasher)
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Filas, default, "Tiza2026!");

        Assert.Equal(2, resultado.Creados);
        Assert.Empty(data.Pendientes);
        Assert.Equal(new[] { "ana@example.test", "juan@example.test" }, data.Activos.Select(a => a.Email));
        Assert.All(data.Activos, a => Assert.Equal("hash:Tiza2026!", a.Hash));
        Assert.Equal(1, hasher.Llamadas); // se hashea una sola vez para todo el archivo
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SinContrasenaInicial_SigueElAltaPendienteConActivacion(string? contrasena)
    {
        var data = new DataAccess();

        var resultado = await new ImportarAlumnosHandler(data, new HasherContador())
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Filas, default, contrasena);

        Assert.Equal(2, resultado.Creados);
        Assert.Empty(data.Activos);
        Assert.Equal(2, data.Pendientes.Count);
    }

    [Theory]
    [InlineData("corta1")]
    [InlineData("ééééééééééééééééééééééééééééééééééééé")] // 37 caracteres = 74 bytes
    [InlineData("        ")] // solo espacios (8): antes daba 500    
    public void ContrasenaFueraDeLasReglas_Invalida(string contrasena)
    {
        Assert.NotNull(ImportarAlumnosHandler.ValidarContrasenaInicial(contrasena));
    }

    [Fact]
    public void ContrasenaValida_SinMotivo()
    {
        Assert.Null(ImportarAlumnosHandler.ValidarContrasenaInicial("Tiza2026!"));
    }

    [Fact]
    public async Task ContrasenaInvalidaEnElHandler_NoCreaANadie()
    {
        var data = new DataAccess();

        await Assert.ThrowsAsync<ArgumentException>(() => new ImportarAlumnosHandler(data, new HasherContador())
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Filas, default, "corta1"));

        Assert.Empty(data.Activos);
        Assert.Empty(data.Pendientes);
    }
}
