using Microsoft.EntityFrameworkCore;
using Npgsql;
using PEI.Evaluaciones.Domain.Entities;
using PEI.Evaluaciones.Infrastructure.Persistence;
using PEI.Evaluaciones.Application.Validacion;

namespace QA07;

public static class QaSupport
{
    public const string Questions = """
        {"version":1,"preguntas":[{"id":"77777777-7777-4777-8777-777777777777","enunciado":"Explica una transaccion","tipo":"Abierta","puntajeMaximo":1,"rubrica":{"criterios":[{"descripcion":"Consistencia","puntaje":1}]}}]}
        """;

    public static EvaluacionDbContext Open(string database)
    {
        var connection = Environment.GetEnvironmentVariable("QA07_CONNECTION")
            ?? throw new InvalidOperationException("Falta QA07_CONNECTION.");
        var cs = new NpgsqlConnectionStringBuilder(connection);
        Require(database.StartsWith("pei_evaluaciones_qa07", StringComparison.Ordinal)
            && cs.Database == database, "Destino fuera de la base exclusiva QA-07.");
        return new(new DbContextOptionsBuilder<EvaluacionDbContext>().UseNpgsql(connection).Options);
    }

    public static async Task VerifyDatabase(EvaluacionDbContext db, string database)
    {
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT current_database()";
        Require((string?)await command.ExecuteScalarAsync() == database, "Base efectiva incorrecta.");
        Require(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Hay migraciones pendientes.");
    }

    public static Evaluacion New(Guid institution) => new()
    {
        Id = Guid.NewGuid(), InstitucionId = institution, Titulo = "QA-07 " + Guid.NewGuid(),
        Tipo = "Examen", PreguntasJson = Questions, Version = 1,
        Estado = EstadoEvaluacion.Borrador, FechaCreacion = DateTimeOffset.UtcNow,
        FechaActualizacion = DateTimeOffset.UtcNow
    };

    public static void Initial(Evaluacion row, Guid institution)
    {
        Require(row.InstitucionId == institution && row.Titulo.StartsWith("QA-07 "), "Registro ajeno a QA-07.");
        Require(row.Estado == EstadoEvaluacion.Borrador && row.Version == 1 && row.CohorteId == null, "Estado inicial incorrecto.");
        Require(new ValidadorPreguntas().Validar(row.PreguntasJson).EsValido, "Preguntas invalidas.");
    }

    public static void Final(Evaluacion before, Evaluacion after, Guid cohort)
    {
        Require(after.Estado == EstadoEvaluacion.Publicada && after.Version == before.Version + 1
            && after.CohorteId == cohort, "Publicacion/version/cohorte incorrecta.");
        Require(after.Id == before.Id && after.InstitucionId == before.InstitucionId
            && after.Titulo == before.Titulo && after.Tipo == before.Tipo
            && after.PreguntasJson == before.PreguntasJson && after.FechaCreacion == before.FechaCreacion
            && after.FechaActualizacion >= before.FechaActualizacion, "Cambio parcial o contenido alterado.");
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
