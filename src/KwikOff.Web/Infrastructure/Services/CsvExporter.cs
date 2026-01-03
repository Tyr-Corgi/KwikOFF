using System.Globalization;
using System.Text;
using CsvHelper;
using KwikOff.Web.Domain.Enums;
using KwikOff.Web.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Exports comparison results to CSV.
/// </summary>
public class CsvExporter : ICsvExporter
{
    private readonly AppDbContext _dbContext;
    private readonly IImageUrlService? _imageUrlService;
    private readonly ILogger<CsvExporter> _logger;

    public CsvExporter(
        AppDbContext dbContext,
        ILogger<CsvExporter> logger,
        IImageUrlService? imageUrlService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _imageUrlService = imageUrlService;
    }

    public async Task<byte[]> ExportMatchedProductsAsync(
        string tenantId,
        Guid? batchId = null,
        MatchStatus? status = null,
        IEnumerable<string>? fields = null,
        bool includeImages = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ComparisonResults
            .Include(r => r.ImportedProduct)
            .Include(r => r.OpenFoodFactsProduct)
            .Where(r => r.ImportedProduct.TenantId == tenantId);

        if (batchId.HasValue)
        {
            query = query.Where(r => r.ImportedProduct.ImportBatchId == batchId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.MatchStatus == status.Value);
        }

        var results = await query.ToListAsync(cancellationToken);

        // Fetch image URLs if requested
        Dictionary<string, ProductImageUrls?>? imageUrlsCache = null;
        if (includeImages && _imageUrlService != null)
        {
            _logger.LogInformation("Fetching image URLs for {Count} products", results.Count);
            imageUrlsCache = new Dictionary<string, ProductImageUrls?>();
            
            var barcodes = results
                .Where(r => r.OpenFoodFactsProduct != null)
                .Select(r => r.OpenFoodFactsProduct!.Barcode)
                .Distinct()
                .ToList();

            _logger.LogInformation("Processing {BarcodeCount} unique barcodes for image URLs", barcodes.Count);

            int processed = 0;
            int skipped = 0;
            foreach (var barcode in barcodes)
            {
                processed++;
                try
                {
                    // Check if images are already cached in the database
                    var offProduct = results
                        .FirstOrDefault(r => r.OpenFoodFactsProduct?.Barcode == barcode)
                        ?.OpenFoodFactsProduct;
                    
                    if (offProduct != null && !string.IsNullOrEmpty(offProduct.ImageUrl))
                    {
                        // Images already cached in database - use them
                        imageUrlsCache[barcode] = new ProductImageUrls
                        {
                            ImageUrl = offProduct.ImageUrl,
                            ImageSmallUrl = offProduct.ImageSmallUrl,
                            ImageFrontUrl = offProduct.ImageFrontUrl,
                            ImageIngredientsUrl = offProduct.ImageIngredientsUrl,
                            ImageNutritionUrl = offProduct.ImageNutritionUrl
                        };
                        skipped++;
                        
                        if (processed % 100 == 0)
                        {
                            _logger.LogInformation("Progress: {Processed}/{Total} barcodes processed ({Skipped} from cache)", 
                                processed, barcodes.Count, skipped);
                        }
                        continue;
                    }

                    // Fetch from API
                    _logger.LogInformation("Fetching image URLs from API for barcode {Barcode} ({Processed}/{Total})", 
                        barcode, processed, barcodes.Count);
                    
                    var imageUrls = await _imageUrlService.FetchAndCacheImageUrlsAsync(barcode, cancellationToken);
                    imageUrlsCache[barcode] = imageUrls;
                    
                    if (processed % 10 == 0)
                    {
                        _logger.LogInformation("Progress: {Processed}/{Total} barcodes processed ({Skipped} from cache, {Fetched} from API)", 
                            processed, barcodes.Count, skipped, processed - skipped);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch image URLs for barcode {Barcode}", barcode);
                    imageUrlsCache[barcode] = null;
                }
            }
            
            _logger.LogInformation("Image URL fetching complete: {Total} barcodes, {Cached} from cache, {Fetched} from API", 
                barcodes.Count, skipped, processed - skipped);
        }

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write headers
        var selectedFields = fields?.ToList() ?? GetDefaultFields();
        
        // Add image fields if requested
        if (includeImages)
        {
            selectedFields.Add("image_url");
            selectedFields.Add("image_small_url");
            selectedFields.Add("image_front_url");
            selectedFields.Add("image_ingredients_url");
            selectedFields.Add("image_nutrition_url");
        }

        foreach (var field in selectedFields)
        {
            csv.WriteField(field);
        }
        await csv.NextRecordAsync();

        // Write data
        foreach (var result in results)
        {
            foreach (var field in selectedFields)
            {
                if (field.StartsWith("image_") && imageUrlsCache != null && result.OpenFoodFactsProduct != null)
                {
                    // Get cached image URLs
                    var barcode = result.OpenFoodFactsProduct.Barcode;
                    if (imageUrlsCache.TryGetValue(barcode, out var imageUrls) && imageUrls != null)
                    {
                        var value = field switch
                        {
                            "image_url" => imageUrls.ImageUrl ?? "",
                            "image_small_url" => imageUrls.ImageSmallUrl ?? "",
                            "image_front_url" => imageUrls.ImageFrontUrl ?? "",
                            "image_ingredients_url" => imageUrls.ImageIngredientsUrl ?? "",
                            "image_nutrition_url" => imageUrls.ImageNutritionUrl ?? "",
                            _ => ""
                        };
                        csv.WriteField(value);
                    }
                    else
                    {
                        csv.WriteField("");
                    }
                }
                else
                {
                    csv.WriteField(GetFieldValue(result, field));
                }
            }
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(cancellationToken);
        return memoryStream.ToArray();
    }

    private static List<string> GetDefaultFields() => new()
    {
        "Barcode",
        "ProductName",
        "Brand",
        "Category",
        "MatchStatus",
        "ConfidenceScore",
        "OFF_ProductName",
        "OFF_Brand",
        "OFF_Categories",
        "OFF_NutritionGrade",
        "HasDiscrepancies"
    };

    private static string GetFieldValue(Domain.Entities.ComparisonResult result, string field)
    {
        return field switch
        {
            // Imported Product Fields
            "Barcode" => result.ImportedProduct?.Barcode ?? "",
            "ProductName" => result.ImportedProduct?.ProductName ?? "",
            "Brand" => result.ImportedProduct?.Brand ?? "",
            "Category" => result.ImportedProduct?.Category ?? "",
            "Description" => result.ImportedProduct?.Description ?? "",
            "Price" => result.ImportedProduct?.Price?.ToString() ?? "",
            "Supplier" => result.ImportedProduct?.Supplier ?? "",
            "Allergens" => result.ImportedProduct?.Allergens ?? "",
            "TraceabilityLotCode" => result.ImportedProduct?.TraceabilityLotCode ?? "",
            
            // Comparison Fields
            "MatchStatus" => result.MatchStatus.ToString(),
            "ConfidenceScore" => result.ConfidenceScore.ToString("P0"),
            "HasDiscrepancies" => (result.HasNameDiscrepancy || result.HasBrandDiscrepancy ||
                                   result.HasCategoryDiscrepancy || result.HasAllergenDiscrepancy).ToString(),
            "Names_Match" => string.Equals(
                result.ImportedProduct?.ProductName,
                result.OpenFoodFactsProduct?.ProductName,
                StringComparison.OrdinalIgnoreCase) ? "Yes" : "No",
            
            // OFF Basic Info
            "OFF_ProductName" => result.OpenFoodFactsProduct?.ProductName ?? "",
            "OFF_GenericName" => result.OpenFoodFactsProduct?.GenericName ?? "",
            "OFF_Brands" => result.OpenFoodFactsProduct?.Brands ?? "",
            "OFF_Quantity" => result.OpenFoodFactsProduct?.Quantity ?? "",
            
            // OFF Categories
            "OFF_Categories" => result.OpenFoodFactsProduct?.Categories ?? "",
            "OFF_CategoriesTags" => result.OpenFoodFactsProduct?.CategoriesTags ?? "",
            
            // OFF Ingredients & Allergens
            "OFF_IngredientsText" => result.OpenFoodFactsProduct?.IngredientsText ?? "",
            "OFF_Allergens" => result.OpenFoodFactsProduct?.Allergens ?? "",
            "OFF_AllergensTags" => result.OpenFoodFactsProduct?.AllergensTags ?? "",
            "OFF_Traces" => result.OpenFoodFactsProduct?.Traces ?? "",
            "OFF_TracesTags" => result.OpenFoodFactsProduct?.TracesTags ?? "",
            
            // OFF Nutrition Scores
            "OFF_NutritionGrades" => result.OpenFoodFactsProduct?.NutritionGrades ?? "",
            "OFF_NovaGroup" => result.OpenFoodFactsProduct?.NovaGroup ?? "",
            "OFF_Ecoscore" => result.OpenFoodFactsProduct?.Ecoscore ?? "",
            
            // OFF Nutritional Values (per 100g)
            "OFF_EnergyKcal100g" => result.OpenFoodFactsProduct?.EnergyKcal100g?.ToString() ?? "",
            "OFF_Fat100g" => result.OpenFoodFactsProduct?.Fat100g?.ToString() ?? "",
            "OFF_SaturatedFat100g" => result.OpenFoodFactsProduct?.SaturatedFat100g?.ToString() ?? "",
            "OFF_Carbohydrates100g" => result.OpenFoodFactsProduct?.Carbohydrates100g?.ToString() ?? "",
            "OFF_Sugars100g" => result.OpenFoodFactsProduct?.Sugars100g?.ToString() ?? "",
            "OFF_Fiber100g" => result.OpenFoodFactsProduct?.Fiber100g?.ToString() ?? "",
            "OFF_Proteins100g" => result.OpenFoodFactsProduct?.Proteins100g?.ToString() ?? "",
            "OFF_Salt100g" => result.OpenFoodFactsProduct?.Salt100g?.ToString() ?? "",
            "OFF_Sodium100g" => result.OpenFoodFactsProduct?.Sodium100g?.ToString() ?? "",
            
            // OFF Serving Size
            "OFF_ServingSize" => result.OpenFoodFactsProduct?.ServingSize ?? "",
            "OFF_ServingQuantity" => result.OpenFoodFactsProduct?.ServingQuantity?.ToString() ?? "",
            
            // OFF Labels & Certifications
            "OFF_Labels" => result.OpenFoodFactsProduct?.Labels ?? "",
            "OFF_LabelsTags" => result.OpenFoodFactsProduct?.LabelsTags ?? "",
            
            // OFF Distribution
            "OFF_Stores" => result.OpenFoodFactsProduct?.Stores ?? "",
            "OFF_Countries" => result.OpenFoodFactsProduct?.Countries ?? "",
            "OFF_CountriesTags" => result.OpenFoodFactsProduct?.CountriesTags ?? "",
            
            // OFF Images (stored in database)
            "OFF_ImageUrl" => result.OpenFoodFactsProduct?.ImageUrl ?? "",
            "OFF_ImageSmallUrl" => result.OpenFoodFactsProduct?.ImageSmallUrl ?? "",
            "OFF_ImageFrontUrl" => result.OpenFoodFactsProduct?.ImageFrontUrl ?? "",
            "OFF_ImageIngredientsUrl" => result.OpenFoodFactsProduct?.ImageIngredientsUrl ?? "",
            "OFF_ImageNutritionUrl" => result.OpenFoodFactsProduct?.ImageNutritionUrl ?? "",
            
            // OFF Packaging
            "OFF_Packaging" => result.OpenFoodFactsProduct?.Packaging ?? "",
            "OFF_PackagingTags" => result.OpenFoodFactsProduct?.PackagingTags ?? "",
            
            // OFF Origin & Manufacturing
            "OFF_Origins" => result.OpenFoodFactsProduct?.Origins ?? "",
            "OFF_OriginsTags" => result.OpenFoodFactsProduct?.OriginsTags ?? "",
            "OFF_ManufacturingPlaces" => result.OpenFoodFactsProduct?.ManufacturingPlaces ?? "",
            
            // OFF Metadata
            "OFF_Completeness" => result.OpenFoodFactsProduct?.Completeness?.ToString() ?? "",
            "OFF_CreatedAt" => result.OpenFoodFactsProduct?.CreatedAt?.ToString("yyyy-MM-dd") ?? "",
            "OFF_LastModified" => result.OpenFoodFactsProduct?.LastModified?.ToString("yyyy-MM-dd") ?? "",
            
            _ => ""
        };
    }
}
