using Logistics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Read-only: never creates databases, applies migrations, seeds data or performs a restore.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
var apiDirectory = Path.Combine(root, "src", "Logistics.Api");
string? connectionString = null;
foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
{
    using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(apiDirectory, name)));
    if (config.RootElement.TryGetProperty("ConnectionStrings", out var strings)
        && strings.TryGetProperty("LogisticsDatabase", out var value))
        connectionString = value.GetString();
}
var configured = new SqlConnectionStringBuilder(connectionString
    ?? throw new InvalidOperationException("ConnectionStrings:LogisticsDatabase is missing."));
if (!configured.IntegratedSecurity || configured.DataSource != @"(localdb)\MSSQLLocalDB")
    throw new InvalidOperationException("Verification is restricted to Windows authentication and MSSQLLocalDB.");
Console.WriteLine($"Configured instance={configured.DataSource}; database={configured.InitialCatalog}; Windows authentication={configured.IntegratedSecurity}");
var originalDirectory = Directory.GetCurrentDirectory();
try
{
    foreach (var directory in new[] { root, apiDirectory, Path.Combine(root, "src", "Logistics.Infrastructure") })
    {
        Directory.SetCurrentDirectory(directory);
        using var designContext = new LogisticsDbContextFactory().CreateDbContext([]);
        var designConnection = new SqlConnectionStringBuilder(designContext.Database.GetConnectionString());
        if (designConnection.ConnectionString != configured.ConnectionString)
            throw new InvalidOperationException($"Design-time configuration differs from API settings at {directory}.");
        Console.WriteLine($"Design-time configuration SUCCESS from {Path.GetRelativePath(root, directory)}");
    }
}
finally { Directory.SetCurrentDirectory(originalDirectory); }
foreach (var trust in new[] { false, true })
{
    var master = new SqlConnectionStringBuilder(configured.ConnectionString)
        { InitialCatalog = "master", TrustServerCertificate = trust, ConnectTimeout = 15 };
    using var connection = new SqlConnection(master.ConnectionString);
    try
    {
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME(), ORIGINAL_LOGIN(), @@SERVERNAME";
        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        Console.WriteLine($"MASTER TrustServerCertificate={trust}: SUCCESS Database={reader.GetString(0)} WindowsLogin={reader.GetString(1)} Instance={reader.GetString(2)}");
    }
    catch (SqlException ex) { Console.WriteLine($"MASTER TrustServerCertificate={trust}: SQL ERROR {ex.Number}: {ex.Message}"); }
}
using var context = new LogisticsDbContext(new DbContextOptionsBuilder<LogisticsDbContext>()
    .UseSqlServer(configured.ConnectionString).Options);
Console.WriteLine($"Provider={context.Database.ProviderName}; compiled migrations={context.Database.GetMigrations().Count()}");
var tables = context.Model.GetRelationalModel().Tables.ToArray();
var inventory = new
{
    Database = configured.InitialCatalog,
    Source = "EF model inferred from legacy source; NOT a live database inventory",
    Tables = tables.Select(t => new
    {
        t.Name, t.Schema,
        Columns = t.Columns.Select(c => new { c.Name, c.StoreType, c.IsNullable }),
        Constraints = t.UniqueConstraints.Select(k => new { k.Name, Columns = k.Columns.Select(c => c.Name) }),
        Indexes = t.Indexes.Select(i => new { i.Name, i.IsUnique, Columns = i.Columns.Select(c => c.Name) }),
        ForeignKeys = t.ForeignKeyConstraints.Select(f => new
        {
            f.Name, TargetTable = f.PrincipalTable.Name, Columns = f.Columns.Select(c => c.Name),
            TargetColumns = f.PrincipalColumns.Select(c => c.Name), DeleteAction = f.OnDeleteAction.ToString()
        })
    })
};
Directory.CreateDirectory(Path.Combine(root, "Database", "Reports"));
File.WriteAllText(Path.Combine(root, "Database", "Reports", "EfModelInventory.json"),
    JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"EF model tables={tables.Length}; foreign keys={tables.Sum(t => t.ForeignKeyConstraints.Count())}; indexes={tables.Sum(t => t.Indexes.Count())}");
try
{
    await context.Database.OpenConnectionAsync();
    using var command = context.Database.GetDbConnection().CreateCommand();
    command.CommandText = "SELECT DB_NAME(), ORIGINAL_LOGIN()";
    using (var reader = await command.ExecuteReaderAsync())
    {
        await reader.ReadAsync();
        Console.WriteLine($"TARGET SUCCESS Database={reader.GetString(0)} WindowsLogin={reader.GetString(1)}");
    }
    Console.WriteLine($"Applied migrations={string.Join(",", await context.Database.GetAppliedMigrationsAsync())}");
    Console.WriteLine($"Pending migrations={string.Join(",", await context.Database.GetPendingMigrationsAsync())}");
    return 0;
}
catch (Exception ex) when (ex.GetBaseException() is SqlException)
{
    var sql = (SqlException)ex.GetBaseException();
    Console.WriteLine($"TARGET SQL ERROR {sql.Number}: {sql.Message}");
    return 2;
}
