using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for KwikOff application.
/// Uses PostgreSQL with JSONB support for flexible data storage.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Core product tables
    public DbSet<ImportedProduct> ImportedProducts => Set<ImportedProduct>();
    public DbSet<OpenFoodFactsProduct> OpenFoodFactsProducts => Set<OpenFoodFactsProduct>();
    public DbSet<ComparisonResult> ComparisonResults => Set<ComparisonResult>();

    // Multi-tenant column mapping
    public DbSet<TenantColumnMapping> TenantColumnMappings => Set<TenantColumnMapping>();

    // Sync status tracking
    public DbSet<SyncStatus> SyncStatuses => Set<SyncStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Use snake_case naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Convert table names to snake_case
            entity.SetTableName(ToSnakeCase(entity.GetTableName() ?? entity.ClrType.Name));

            // Convert column names to snake_case
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            // Convert key names to snake_case
            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? "pk_" + entity.GetTableName()));
            }

            // Convert foreign key names to snake_case
            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName() ?? "fk_" + entity.GetTableName()));
            }

            // Convert index names to snake_case
            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? "ix_" + entity.GetTableName()));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
