using System.Text.Json;
using PEI.Identidad.DevelopmentSeed;
using PEI.Identidad.Infrastructure.Security;

// No API configuration, HTTP host, automatic migration or default destination.
if (args.Length != 0)
{
    Console.Error.WriteLine("No se aceptan argumentos. Utilice configuración externa ID08_*.");
    return 2;
}
try
{
    string Read(string key) => Environment.GetEnvironmentVariable("ID08_" + key) ?? "";
    var environment = Read("ENVIRONMENT");
    var options = new SeedOptions
    {
        EnvironmentName = environment,
        Enabled = Read("ENABLED") == "true",
        Operator = Read("OPERATOR"), Schema = Read("SCHEMA"),
        ConnectionString = environment == "Testing"
            ? Environment.GetEnvironmentVariable("PEI_IDENTIDAD_TEST_CONNECTION") ?? ""
            : Read("CONNECTION"),
        AllowedDestinations = JsonSerializer.Deserialize<SeedDestination[]>(Read("ALLOWED_DESTINATIONS")) ?? [],
        Passwords = SeedCatalog.Users.Select(user => (user.Id, Password: Read("PASSWORD_" + user.Id.ToString("N"))))
            .Where(pair => pair.Password.Length > 0).ToDictionary(pair => pair.Id, pair => pair.Password)
    };
    var report = await new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(options);
    Console.WriteLine($"{report.CatalogVersion}: creados={report.Created}; omitidos={report.Skipped}; conflictos={report.Conflicts}.");
    return 0;
}
catch (SeedRejectedException ex)
{
    Console.Error.WriteLine($"Carga cancelada: {ex.Message} No se confirmaron cambios.");
    return 2;
}
catch (Exception)
{
    // PostgreSQL errors can contain row values, hashes, connection details or supplied secrets.
    Console.Error.WriteLine("Carga cancelada por error técnico. No se confirmaron cambios. Diagnóstico sensible no publicado.");
    return 1;
}
