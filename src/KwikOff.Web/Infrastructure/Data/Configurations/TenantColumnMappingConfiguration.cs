using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KwikOff.Web.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for TenantColumnMapping entity.
/// </summary>
public class TenantColumnMappingConfiguration : IEntityTypeConfiguration<TenantColumnMapping>
{
    public void Configure(EntityTypeBuilder<TenantColumnMapping> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.FilePattern)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.ColumnMappingJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedByUser)
            .HasMaxLength(100);

        // Unique constraint on tenant + file pattern
        builder.HasIndex(e => new { e.TenantId, e.FilePattern })
            .IsUnique()
            .HasDatabaseName("ix_tenant_column_mappings_tenant_pattern");

        // Index for tenant lookups
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_tenant_column_mappings_tenant_id");

        // Index for finding active mappings
        builder.HasIndex(e => new { e.TenantId, e.IsActive })
            .HasDatabaseName("ix_tenant_column_mappings_tenant_active");

        builder.HasIndex(e => e.LastUsedAt)
            .HasDatabaseName("ix_tenant_column_mappings_last_used");
    }
}
