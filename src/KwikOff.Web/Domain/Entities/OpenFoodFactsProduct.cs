namespace KwikOff.Web.Domain.Entities;

/// <summary>
/// Represents a product from the Open Food Facts database.
/// Stores comprehensive product information from the ~3M item database.
/// </summary>
public class OpenFoodFactsProduct
{
    public long Id { get; set; }

    // Identifiers
    public string Barcode { get; set; } = string.Empty;
    public string NormalizedBarcode { get; set; } = string.Empty;

    // Basic product info
    public string? ProductName { get; set; }
    public string? GenericName { get; set; }
    public string? Brands { get; set; }
    public string? Categories { get; set; }
    public string? CategoriesTags { get; set; }

    // Ingredients and allergens
    public string? IngredientsText { get; set; }
    public string? Allergens { get; set; }
    public string? AllergensTags { get; set; }
    public string? Traces { get; set; }
    public string? TracesTags { get; set; }

    // Nutrition data
    public string? NutritionGrades { get; set; }
    public string? NovaGroup { get; set; }
    public string? Ecoscore { get; set; }
    public decimal? EnergyKcal100g { get; set; }
    public decimal? Fat100g { get; set; }
    public decimal? SaturatedFat100g { get; set; }
    public decimal? Carbohydrates100g { get; set; }
    public decimal? Sugars100g { get; set; }
    public decimal? Fiber100g { get; set; }
    public decimal? Proteins100g { get; set; }
    public decimal? Salt100g { get; set; }
    public decimal? Sodium100g { get; set; }

    // Serving size
    public string? ServingSize { get; set; }
    public decimal? ServingQuantity { get; set; }

    // Labels and certifications
    public string? Labels { get; set; }
    public string? LabelsTags { get; set; }
    public string? Stores { get; set; }
    public string? Countries { get; set; }
    public string? CountriesTags { get; set; }

    // Images
    public string? ImageUrl { get; set; }
    public string? ImageSmallUrl { get; set; }
    public string? ImageFrontUrl { get; set; }
    public string? ImageIngredientsUrl { get; set; }
    public string? ImageNutritionUrl { get; set; }

    // Packaging
    public string? Packaging { get; set; }
    public string? PackagingTags { get; set; }
    public string? Quantity { get; set; }

    // Origin and manufacturing
    public string? Origins { get; set; }
    public string? OriginsTags { get; set; }
    public string? ManufacturingPlaces { get; set; }

    // Metadata
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastModified { get; set; }
    public string? Creator { get; set; }
    public string? LastModifiedBy { get; set; }
    public int? Completeness { get; set; }

    // Note: RawJson field was removed to save ~90% disk space (13GB → 1GB per 1.5M products)
    // If needed in the future, re-import from Open Food Facts
    // public string? RawJson { get; set; }

    // Navigation properties
    public ICollection<ComparisonResult> ComparisonResults { get; set; } = new List<ComparisonResult>();
}
