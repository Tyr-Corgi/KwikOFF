using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KwikOff.Web.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for ComparisonResult entity.
/// </summary>
public class ComparisonResultConfiguration : IEntityTypeConfiguration<ComparisonResult>
{
    public void Configure(EntityTypeBuilder<ComparisonResult> builder)
    {
        builder.HasKey(e => e.Id);

        // Match status stored as integer
        builder.Property(e => e.MatchStatus)
            .HasConversion<int>();

        // Confidence score
        builder.Property(e => e.ConfidenceScore)
            .HasPrecision(5, 4);

        // Comparison details as JSONB
        builder.Property(e => e.ComparisonDetails)
            .HasColumnType("jsonb");

        // Relationships
        builder.HasOne(e => e.ImportedProduct)
            .WithMany(p => p.ComparisonResults)
            .HasForeignKey(e => e.ImportedProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.OpenFoodFactsProduct)
            .WithMany(p => p.ComparisonResults)
            .HasForeignKey(e => e.OpenFoodFactsProductId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.ImportedProductId)
            .HasDatabaseName("ix_comparison_results_imported_product_id");

        builder.HasIndex(e => e.OpenFoodFactsProductId)
            .HasDatabaseName("ix_comparison_results_openfoodfacts_product_id");

        builder.HasIndex(e => e.MatchStatus)
            .HasDatabaseName("ix_comparison_results_match_status");

        builder.HasIndex(e => e.ComparisonBatchId)
            .HasDatabaseName("ix_comparison_results_comparison_batch_id");

        builder.HasIndex(e => e.ComparedAt)
            .HasDatabaseName("ix_comparison_results_compared_at");

        // Composite index for finding latest comparison per imported product
        builder.HasIndex(e => new { e.ImportedProductId, e.ComparedAt })
            .HasDatabaseName("ix_comparison_results_product_date");
    }
}
