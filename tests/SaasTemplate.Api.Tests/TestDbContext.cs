using SaasTemplate.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SaasTemplate.Api.Tests;

/// <summary>
/// SQLite-compatible DbContext for integration tests.
/// Removes SQL Server-specific column type annotations so EnsureCreated works with SQLite.
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Strip SQL Server column types so SQLite EnsureCreated works
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnType = property.GetColumnType();
                if (columnType != null &&
                    (columnType.Contains("nvarchar", StringComparison.OrdinalIgnoreCase) ||
                     columnType.Contains("datetime2", StringComparison.OrdinalIgnoreCase) ||
                     columnType.Contains("uniqueidentifier", StringComparison.OrdinalIgnoreCase)))
                {
                    property.SetColumnType(null);
                }
            }
        }
    }
}
