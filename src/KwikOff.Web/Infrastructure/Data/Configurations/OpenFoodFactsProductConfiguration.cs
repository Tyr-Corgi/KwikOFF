using KwikOff.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KwikOff.Web.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for OpenFoodFactsProduct entity.
/// Optimized for the ~3M product database with proper indexing.
/// </summary>
public class OpenFoodFactsProductConfiguration : IEntityTypeConfiguration<OpenFoodFactsProduct>
{
    public void Configure(EntityTypeBuilder<OpenFoodFactsProduct> builder)
    {
        builder.HasKey(e => e.Id);

        // Identifiers
        builder.Property(e => e.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.NormalizedBarcode)
            .IsRequired()
            .HasMaxLength(50);

        // Product info - 10k limit to handle any real-world data
        builder.Property(e => e.ProductName).HasMaxLength(10000);
        builder.Property(e => e.GenericName).HasMaxLength(10000);
        builder.Property(e => e.Brands).HasMaxLength(10000);
        builder.Property(e => e.Categories).HasMaxLength(10000);
        builder.Property(e => e.CategoriesTags).HasMaxLength(10000);

        // Ingredients and allergens - 10k limit
        builder.Property(e => e.IngredientsText).HasMaxLength(10000);
        builder.Property(e => e.Allergens).HasMaxLength(10000);
        builder.Property(e => e.AllergensTags).HasMaxLength(10000);
        builder.Property(e => e.Traces).HasMaxLength(10000);
        builder.Property(e => e.TracesTags).HasMaxLength(10000);

        // Nutrition grades - increased length for unexpected values
        builder.Property(e => e.NutritionGrades).HasMaxLength(50);
        builder.Property(e => e.NovaGroup).HasMaxLength(50);
        builder.Property(e => e.Ecoscore).HasMaxLength(50);

        // Nutrition values with precision
        builder.Property(e => e.EnergyKcal100g).HasPrecision(10, 2);
        builder.Property(e => e.Fat100g).HasPrecision(10, 2);
        builder.Property(e => e.SaturatedFat100g).HasPrecision(10, 2);
        builder.Property(e => e.Carbohydrates100g).HasPrecision(10, 2);
        builder.Property(e => e.Sugars100g).HasPrecision(10, 2);
        builder.Property(e => e.Fiber100g).HasPrecision(10, 2);
        builder.Property(e => e.Proteins100g).HasPrecision(10, 2);
        builder.Property(e => e.Salt100g).HasPrecision(10, 2);
        builder.Property(e => e.Sodium100g).HasPrecision(10, 2);
        builder.Property(e => e.ServingQuantity).HasPrecision(10, 2);

        // Serving size
        builder.Property(e => e.ServingSize).HasMaxLength(500);

        // Labels - 10k limit
        builder.Property(e => e.Labels).HasMaxLength(10000);
        builder.Property(e => e.LabelsTags).HasMaxLength(10000);
        builder.Property(e => e.Stores).HasMaxLength(10000);
        builder.Property(e => e.Countries).HasMaxLength(10000);
        builder.Property(e => e.CountriesTags).HasMaxLength(10000);

        // Images (URLs) - 10k limit
        builder.Property(e => e.ImageUrl).HasMaxLength(10000);
        builder.Property(e => e.ImageSmallUrl).HasMaxLength(10000);
        builder.Property(e => e.ImageFrontUrl).HasMaxLength(10000);
        builder.Property(e => e.ImageIngredientsUrl).HasMaxLength(10000);
        builder.Property(e => e.ImageNutritionUrl).HasMaxLength(10000);

        // Packaging - 10k limit
        builder.Property(e => e.Packaging).HasMaxLength(10000);
        builder.Property(e => e.PackagingTags).HasMaxLength(10000);
        builder.Property(e => e.Quantity).HasMaxLength(500);

        // Origin - 10k limit
        builder.Property(e => e.Origins).HasMaxLength(10000);
        builder.Property(e => e.OriginsTags).HasMaxLength(10000);
        builder.Property(e => e.ManufacturingPlaces).HasMaxLength(10000);

        // Metadata
        builder.Property(e => e.Creator).HasMaxLength(500);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(500);

        // Note: RawJson column removed to save ~90% disk space
        // Re-import from Open Food Facts if needed in the future

        // Critical indexes for barcode lookups (most important for 3M+ records)
        builder.HasIndex(e => e.NormalizedBarcode)
            .IsUnique()
            .HasDatabaseName("ix_openfoodfacts_products_normalized_barcode");

        builder.HasIndex(e => e.Barcode)
            .HasDatabaseName("ix_openfoodfacts_products_barcode");

        // Additional indexes for search functionality
        builder.HasIndex(e => e.Brands)
            .HasDatabaseName("ix_openfoodfacts_products_brands");

        builder.HasIndex(e => e.NutritionGrades)
            .HasDatabaseName("ix_openfoodfacts_products_nutrition_grades");

        builder.HasIndex(e => e.LastModified)
            .HasDatabaseName("ix_openfoodfacts_products_last_modified");
    }
}
