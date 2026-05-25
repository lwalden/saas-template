using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaasTemplate.Api.Data;

/// <summary>
/// Design-time factory used by EF Core tools (migrations add, etc.) when the
/// full app host isn't available. Reads DefaultConnection from the repo-root .env
/// file, falling back to a local SQL Server default so developers don't need
/// environment config just to scaffold a migration.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadEnvConnectionString()
            ?? "Server=(localdb)\\mssqllocaldb;Database=SaasTemplate;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static string? ReadEnvConnectionString()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var envFile = Path.Combine(dir.FullName, ".env");
            if (File.Exists(envFile))
            {
                foreach (var line in File.ReadAllLines(envFile))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..].Trim();
                    if (key.Equals("ConnectionStrings__DefaultConnection", StringComparison.OrdinalIgnoreCase)
                        || key.Equals("DefaultConnection", StringComparison.OrdinalIgnoreCase))
                        return value;
                }
                break;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
