using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KwikOff.Web.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for SyncStatus entity.
/// </summary>
public class SyncStatusConfiguration : IEntityTypeConfiguration<SyncStatus>
{
    public void Configure(EntityTypeBuilder<SyncStatus> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.StatusMessage)
            .HasMaxLength(500);

        builder.Property(e => e.LastError)
            .HasMaxLength(2000);

        // Unique constraint on source name (typically only one sync status per source)
        builder.HasIndex(e => e.SourceName)
            .IsUnique()
            .HasDatabaseName("ix_sync_statuses_source_name");

        // Index for finding syncing operations
        builder.HasIndex(e => e.IsSyncing)
            .HasDatabaseName("ix_sync_statuses_is_syncing");
    }
}
