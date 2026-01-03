using KwikOff.Web.Domain.Enums;

namespace KwikOff.Web.Domain.Entities;

/// <summary>
/// Represents the result of comparing an imported product against Open Food Facts.
/// Tracks match status and any discrepancies found.
/// </summary>
public class ComparisonResult
{
    public long Id { get; set; }

    // Foreign keys
    public long ImportedProductId { get; set; }
    public long? OpenFoodFactsProductId { get; set; }

    // Match status
    public MatchStatus MatchStatus { get; set; }
    public double ConfidenceScore { get; set; }

    // Comparison details stored as JSONB
    public string? ComparisonDetails { get; set; }

    // Secondary search tracking
    public bool UsedSecondarySearch { get; set; }
    public string? SecondarySearchMethod { get; set; }

    // Discrepancy tracking
    public bool HasNameDiscrepancy { get; set; }
    public bool HasBrandDiscrepancy { get; set; }
    public bool HasCategoryDiscrepancy { get; set; }
    public bool HasAllergenDiscrepancy { get; set; }
    public bool HasNutritionDiscrepancy { get; set; }

    // Timestamps
    public DateTime ComparedAt { get; set; } = DateTime.UtcNow;
    public Guid ComparisonBatchId { get; set; }

    // Navigation properties
    public ImportedProduct ImportedProduct { get; set; } = null!;
    public OpenFoodFactsProduct? OpenFoodFactsProduct { get; set; }
}
