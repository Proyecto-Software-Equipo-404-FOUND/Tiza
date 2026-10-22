using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PEI.Identidad.Infrastructure.Persistence;

public class PlataformaDbContextFactory
    : IDesignTimeDbContextFactory<PlataformaDbContext>
{
    public PlataformaDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("IdentidadDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configurar ConnectionStrings__IdentidadDb para ejecutar migraciones de PlataformaDbContext.");
        }

        var options = new DbContextOptionsBuilder<PlataformaDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistoryPlataforma"))
            .Options;

        return new PlataformaDbContext(options);
    }
}
