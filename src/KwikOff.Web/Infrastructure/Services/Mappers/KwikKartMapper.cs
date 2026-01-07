using KwikOff.Web.Features.Comparison;
using System.Text.Json;

namespace KwikOff.Web.Infrastructure.Services.Mappers;

/// <summary>
/// Maps KwikOFF enriched data to KwikKart API format
/// Simplified version using only basic comparison result fields
/// </summary>
public class KwikKartMapper
{
    /// <summary>
    /// Maps comparison results to KwikKart reconciliation request format
    /// </summary>
    public KwikKartReconciliationRequest MapToReconciliationRequest(
        List<ComparisonResultDto> results,
        string tenantId)
    {
        var products = results
            .Where(r => r.MatchStatus == Domain.Enums.MatchStatus.Matched)
            .Select(r => MapToKwikKartProduct(r))
            .Where(p => p != null)
            .ToList();

        return new KwikKartReconciliationRequest
        {
            TenantId = tenantId,
            Products = products!,
            EnrichmentSource = "OPENFOODFACTS",
            ProcessedDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps a single comparison result to KwikKart product format
    /// </summary>
    public KwikKartProduct? MapToKwikKartProduct(ComparisonResultDto result)
    {
        if (result.MatchStatus != Domain.Enums.MatchStatus.Matched)
            return null;

        // Create base product
        var product = new KwikKartProduct
        {
            GTIN = result.OffBarcode ?? result.Barcode,
            ProductName = result.OffProductName ?? result.ProductName,
            Brand = result.OffBrand,
            CompletenessScore = CalculateCompletenessScore(result),
            ProductStatus = "PENDING", // Will be activated in KwikKart after review
            DataSource = "OPENFOODFACTS",
            EnrichedFields = new List<string>(),
            MatchConfidence = result.ConfidenceScore
        };

        // Add image if available
        if (!string.IsNullOrEmpty(result.ImageUrl))
        {
            product.ImageUrl = result.ImageUrl;
            product.EnrichedFields.Add("image");
        }

        // Note: Full nutrition and allergen data would require
        // querying the OpenFoodFactsProduct table separately
        // This simplified version only includes data from ComparisonResultDto

        return product;
    }

    private decimal CalculateCompletenessScore(ComparisonResultDto result)
    {
        var totalFields = 5m;
        var completedFields = 0m;

        if (!string.IsNullOrEmpty(result.OffProductName)) completedFields++;
        if (!string.IsNullOrEmpty(result.OffBrand)) completedFields++;
        if (!string.IsNullOrEmpty(result.OffBarcode)) completedFields++;
        if (!string.IsNullOrEmpty(result.ImageUrl)) completedFields++;
        if (result.ConfidenceScore >= 0.9) completedFields++; // High confidence match

        return Math.Round((completedFields / totalFields) * 100, 2);
    }
}

// KwikKart API DTOs
public class KwikKartReconciliationRequest
{
    public string TenantId { get; set; } = string.Empty;
    public List<KwikKartProduct> Products { get; set; } = new();
    public string EnrichmentSource { get; set; } = string.Empty;
    public DateTime ProcessedDate { get; set; }
}

public class KwikKartProduct
{
    public string GTIN { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string ProductStatus { get; set; } = "PENDING";
    public decimal CompletenessScore { get; set; }
    public double MatchConfidence { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public List<string> EnrichedFields { get; set; } = new();
    public string? ImageUrl { get; set; }
}
