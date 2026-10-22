using Npgsql;

namespace PEI.Identidad.DevelopmentSeed;

public sealed record SeedDestination(string Host, int Port, string Database, string Schema);

public sealed class SeedOptions
{
    public required string EnvironmentName { get; init; }
    public required bool Enabled { get; init; }
    public bool AllowDevelopmentPublic { get; init; }
    public required string ConnectionString { get; init; }
    public required string Schema { get; init; }
    public required string Operator { get; init; }
    public required IReadOnlyList<SeedDestination> AllowedDestinations { get; init; }
    public IReadOnlyDictionary<Guid, string> Passwords { get; init; } = new Dictionary<Guid, string>();

    public NpgsqlConnectionStringBuilder Validate()
    {
        if (EnvironmentName is not ("Development" or "Testing") || !Enabled ||
            string.IsNullOrWhiteSpace(Operator) || Operator.Length > 150 || Operator.Any(char.IsControl))
            throw new SeedRejectedException("Entorno, habilitación u operador no autorizados.");
        // Identificador simple: public tiene una excepción acotada y explícita más abajo.
        if (!System.Text.RegularExpressions.Regex.IsMatch(Schema, "^[a-z][a-z0-9_]{0,62}$") ||
            Schema.StartsWith("pg_", StringComparison.Ordinal) || Schema == "information_schema")
            throw new SeedRejectedException("Esquema dedicado no autorizado.");
        NpgsqlConnectionStringBuilder connection;
        try { connection = new(ConnectionString); }
        catch (ArgumentException) { throw new SeedRejectedException("Configuración de conexión inválida."); }
        if (Schema == "public" && (!AllowDevelopmentPublic || EnvironmentName != "Development" ||
            connection.Host != "localhost" || connection.Port != 5541 || connection.Database != "pei_identidad_db"))
            throw new SeedRejectedException("public requiere autorización explícita para Development en localhost:5541/pei_identidad_db.");
        if (!AllowedDestinations.Contains(new SeedDestination(connection.Host ?? "", connection.Port, connection.Database ?? "", Schema)))
            throw new SeedRejectedException("Destino fuera de la lista autorizada.");
        if (EnvironmentName == "Testing" &&
            (connection.Database != "pei_identidad_tests" || connection.Port != 5541 ||
             connection.Host is not ("localhost" or "127.0.0.1" or "::1")))
            throw new SeedRejectedException("Testing requiere pei_identidad_tests local:5541.");
        connection.SearchPath = Schema;
        connection.IncludeErrorDetail = false;
        connection.LogParameters = false;
        // One-shot command: no idle per-schema pools survive a run in the integration fixture.
        connection.Pooling = false;
        return connection;
    }
}

public sealed class SeedRejectedException(string message) : Exception(message);
public sealed record SeedReport(string CatalogVersion, int Created, int Skipped, int Conflicts);
