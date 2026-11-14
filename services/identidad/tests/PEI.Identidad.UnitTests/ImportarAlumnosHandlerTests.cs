using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.UseCases.Usuarios.Importacion;

namespace PEI.Identidad.UnitTests;

public sealed class ImportarAlumnosHandlerTests
{
    private sealed class DataAccess : IImportacionAlumnosDataAccess
    {
        public List<string> Creados { get; } = [];
        public List<string> Intentados { get; } = [];
        public Task<string> NormalizarEmailAsync(string email, CancellationToken ct) => Task.FromResult(email.ToLowerInvariant());
        public Task<ResultadoPersistenciaAlumno> CrearPendienteAsync(Guid admin, Guid institucion, string nombre,
            string apellido, string email, CancellationToken ct)
        {
            Intentados.Add(email);
            if (email == "error@example.test") throw new ApplicationException("fallo de fila");
            var normalized = email.ToLowerInvariant();
            Creados.Add(email);
            return Task.FromResult(new ResultadoPersistenciaAlumno(normalized, ResultadoAltaAlumno.Creado));
        }
    }

    [Fact]
    public async Task InformaErroresDeFilaContinuaYOmiteDuplicados()
    {
        var data = new DataAccess();
        var handler = new ImportarAlumnosHandler(data);
        var filas = new[]
        {
            new FilaAlumnoCsv(2, " Ana ", " Pérez ", "ANA@example.test"),
            new FilaAlumnoCsv(3, "", "Apellido", "vacio@example.test"),
            new FilaAlumnoCsv(4, "Otra", "Persona", "ana@example.test"),
            new FilaAlumnoCsv(5, "Error", "Persistencia", "error@example.test"),
            new FilaAlumnoCsv(6, "DuplicadoDeError", "Archivo", "ERROR@example.test"),
            new FilaAlumnoCsv(7, "Final", "Persona", "final@example.test")
        };

        var result = await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), filas, default);

        Assert.Equal(2, result.Creados);
        Assert.Equal(2, result.Omitidos);
        Assert.Equal(new[] { 3, 5 }, result.Errores.Select(error => error.Fila));
        Assert.Equal(new[] { "ANA@example.test", "final@example.test" }, data.Creados);
        Assert.DoesNotContain("ERROR@example.test", data.Intentados);
    }
}
