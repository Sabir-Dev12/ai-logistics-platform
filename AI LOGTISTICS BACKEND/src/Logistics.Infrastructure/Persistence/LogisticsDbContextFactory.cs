using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Logistics.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling can build the model without booting the web host.
/// </summary>
/// <remarks>
/// Use it to generate the initial migration once the tooling is available:
/// <code>
///   dotnet tool install --global dotnet-ef
///   dotnet ef migrations add InitialCreate \
///       --project src/Logistics.Infrastructure \
///       --startup-project src/Logistics.Api
/// </code>
/// <para>
/// <b>Do not apply the generated migration to the production database unreviewed.</b> Every column,
/// type and length in this model was reverse-engineered from raw SQL literals in the legacy VB code,
/// because the legacy repository contains no DDL. A DBA must reconcile the inferred schema against
/// the live TransmarNetref database (<c>INFORMATION_SCHEMA.COLUMNS</c>) before the two are compared or
/// merged. The initial migration is best used to create a fresh database, not to alter the existing
/// one.
/// </para>
/// </remarks>
public sealed class LogisticsDbContextFactory : IDesignTimeDbContextFactory<LogisticsDbContext>
{
    public LogisticsDbContext CreateDbContext(string[] args)
    {
        // EF tooling may start from the solution root, an individual project, or the API.
        // Resolve the API's actual configuration instead of silently selecting a fallback database.
        string? configurationDirectory = null;
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null; directory = directory.Parent)
        {
            var apiDirectory = Path.Combine(directory.FullName, "src", "Logistics.Api");
            if (File.Exists(Path.Combine(apiDirectory, "appsettings.json")))
            {
                configurationDirectory = apiDirectory;
                break;
            }

            if (File.Exists(Path.Combine(directory.FullName, "Logistics.Api.csproj"))
                && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                configurationDirectory = directory.FullName;
                break;
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configurationDirectory
                ?? throw new InvalidOperationException("Cannot locate the Logistics.Api configuration directory."))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("LogisticsDatabase")
            ?? throw new InvalidOperationException("Connection string 'LogisticsDatabase' is not configured.");

        var options = new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(LogisticsDbContext).Assembly.FullName))
            .Options;

        return new LogisticsDbContext(options);
    }
}
