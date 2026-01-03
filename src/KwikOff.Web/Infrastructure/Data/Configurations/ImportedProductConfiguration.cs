using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KwikOff.Web.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for ImportedProduct entity.
/// Optimized for large imports with proper indexing.
/// </summary>
public class ImportedProductConfiguration : IEntityTypeConfiguration<ImportedProduct>
{
    public void Configure(EntityTypeBuilder<ImportedProduct> builder)
    {
        builder.HasKey(e => e.Id);

        // Required fields
        builder.Property(e => e.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.NormalizedBarcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ProductName)
            .IsRequired()
            .HasMaxLength(500);

        // Optional string fields with max lengths
        builder.Property(e => e.Brand).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Category).HasMaxLength(500);
        builder.Property(e => e.Supplier).HasMaxLength(200);
        builder.Property(e => e.InternalSku).HasMaxLength(100);
        builder.Property(e => e.Allergens).HasMaxLength(1000);
        builder.Property(e => e.UnitOfMeasure).HasMaxLength(50);

        // Pricing with precision
        builder.Property(e => e.Price).HasPrecision(18, 4);
        builder.Property(e => e.SalesPrice).HasPrecision(18, 4);
        builder.Property(e => e.Quantity).HasPrecision(18, 4);

        // FSMA 204 fields
        builder.Property(e => e.TraceabilityLotCode).HasMaxLength(100);
        builder.Property(e => e.OriginLocation).HasMaxLength(500);
        builder.Property(e => e.CurrentLocation).HasMaxLength(500);
        builder.Property(e => e.DestinationLocation).HasMaxLength(500);
        builder.Property(e => e.ReferenceDocumentType).HasMaxLength(100);
        builder.Property(e => e.ReferenceDocumentNumber).HasMaxLength(100);

        // Import metadata
        builder.Property(e => e.TenantId).HasMaxLength(100);
        builder.Property(e => e.FileName).HasMaxLength(500);

        // JSONB for original data
        builder.Property(e => e.OriginalData)
            .HasColumnType("jsonb");

        // Indexes for query performance
        builder.HasIndex(e => e.NormalizedBarcode)
            .HasDatabaseName("ix_imported_products_normalized_barcode");

        builder.HasIndex(e => e.Barcode)
            .HasDatabaseName("ix_imported_products_barcode");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_imported_products_tenant_id");

        builder.HasIndex(e => e.ImportBatchId)
            .HasDatabaseName("ix_imported_products_import_batch_id");

        builder.HasIndex(e => e.Brand)
            .HasDatabaseName("ix_imported_products_brand");

        builder.HasIndex(e => e.TraceabilityLotCode)
            .HasDatabaseName("ix_imported_products_tlc");

        // Composite index for tenant + barcode lookups
        builder.HasIndex(e => new { e.TenantId, e.NormalizedBarcode })
            .HasDatabaseName("ix_imported_products_tenant_barcode");

        builder.HasIndex(e => e.ImportedAt)
            .HasDatabaseName("ix_imported_products_imported_at");
    }
}
