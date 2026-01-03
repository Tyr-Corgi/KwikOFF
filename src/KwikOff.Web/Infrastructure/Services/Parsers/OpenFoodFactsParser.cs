using System.Text.Json;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Services.DataSanitizers;

namespace KwikOff.Web.Infrastructure.Services.Parsers;

/// <summary>
/// Parses Open Food Facts JSON data into domain entities.
/// Handles validation and sanitization of crowdsourced data.
/// </summary>
public class OpenFoodFactsParser
{
    private readonly IBarcodeNormalizer _barcodeNormalizer;
    private readonly ILogger<OpenFoodFactsParser> _logger;

    public OpenFoodFactsParser(IBarcodeNormalizer barcodeNormalizer, ILogger<OpenFoodFactsParser> logger)
    {
        _barcodeNormalizer = barcodeNormalizer;
        _logger = logger;
    }

    /// <summary>
    /// Parses a single JSONL line into an OpenFoodFactsProduct.
    /// Returns null if the product is invalid or cannot be parsed.
    /// </summary>
    public OpenFoodFactsProduct? ParseProduct(string jsonLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;

            // Extract and validate barcode
            var barcode = JsonDataSanitizer.GetString(root, "code") ?? JsonDataSanitizer.GetString(root, "_id");
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length > 50)
                return null;

            var product = new OpenFoodFactsProduct
            {
                Barcode = barcode,
                NormalizedBarcode = _barcodeNormalizer.Normalize(barcode),
                
                // Basic product info
                ProductName = JsonDataSanitizer.GetString(root, "product_name"),
                GenericName = JsonDataSanitizer.GetString(root, "generic_name"),
                Brands = JsonDataSanitizer.GetString(root, "brands"),
                Categories = JsonDataSanitizer.GetString(root, "categories"),
                CategoriesTags = JsonDataSanitizer.GetString(root, "categories_tags"),
                
                // Ingredients and allergens
                IngredientsText = JsonDataSanitizer.GetString(root, "ingredients_text"),
                Allergens = JsonDataSanitizer.GetString(root, "allergens"),
                AllergensTags = JsonDataSanitizer.GetString(root, "allergens_tags"),
                Traces = JsonDataSanitizer.GetString(root, "traces"),
                TracesTags = JsonDataSanitizer.GetString(root, "traces_tags"),
                
                // Nutrition scores
                NutritionGrades = JsonDataSanitizer.GetString(root, "nutrition_grades"),
                NovaGroup = JsonDataSanitizer.GetString(root, "nova_group"),
                Ecoscore = JsonDataSanitizer.GetString(root, "ecoscore_grade"),
                
                // Nutritional values
                EnergyKcal100g = JsonDataSanitizer.GetDecimal(root, "nutriments.energy-kcal_100g"),
                Fat100g = JsonDataSanitizer.GetDecimal(root, "nutriments.fat_100g"),
                SaturatedFat100g = JsonDataSanitizer.GetDecimal(root, "nutriments.saturated-fat_100g"),
                Carbohydrates100g = JsonDataSanitizer.GetDecimal(root, "nutriments.carbohydrates_100g"),
                Sugars100g = JsonDataSanitizer.GetDecimal(root, "nutriments.sugars_100g"),
                Fiber100g = JsonDataSanitizer.GetDecimal(root, "nutriments.fiber_100g"),
                Proteins100g = JsonDataSanitizer.GetDecimal(root, "nutriments.proteins_100g"),
                Salt100g = JsonDataSanitizer.GetDecimal(root, "nutriments.salt_100g"),
                Sodium100g = JsonDataSanitizer.GetDecimal(root, "nutriments.sodium_100g"),
                
                // Serving information
                ServingSize = JsonDataSanitizer.GetString(root, "serving_size"),
                ServingQuantity = JsonDataSanitizer.GetDecimal(root, "serving_quantity"),
                
                // Labels and distribution
                Labels = JsonDataSanitizer.GetString(root, "labels"),
                LabelsTags = JsonDataSanitizer.GetString(root, "labels_tags"),
                Stores = JsonDataSanitizer.GetString(root, "stores"),
                Countries = JsonDataSanitizer.GetString(root, "countries"),
                CountriesTags = JsonDataSanitizer.GetString(root, "countries_tags"),
                
                // Images
                ImageUrl = JsonDataSanitizer.GetString(root, "image_url"),
                ImageSmallUrl = JsonDataSanitizer.GetString(root, "image_small_url"),
                ImageFrontUrl = JsonDataSanitizer.GetString(root, "image_front_url"),
                ImageIngredientsUrl = JsonDataSanitizer.GetString(root, "image_ingredients_url"),
                ImageNutritionUrl = JsonDataSanitizer.GetString(root, "image_nutrition_url"),
                
                // Packaging and quantity
                Packaging = JsonDataSanitizer.GetString(root, "packaging"),
                PackagingTags = JsonDataSanitizer.GetString(root, "packaging_tags"),
                Quantity = JsonDataSanitizer.GetString(root, "quantity"),
                
                // Origin information
                Origins = JsonDataSanitizer.GetString(root, "origins"),
                OriginsTags = JsonDataSanitizer.GetString(root, "origins_tags"),
                ManufacturingPlaces = JsonDataSanitizer.GetString(root, "manufacturing_places"),
                
                // Metadata
                Creator = JsonDataSanitizer.GetString(root, "creator", 500),
                LastModifiedBy = JsonDataSanitizer.GetString(root, "last_modified_by", 500),
                CreatedAt = JsonDataSanitizer.GetDateTime(root, "created_t"),
                LastModified = JsonDataSanitizer.GetDateTime(root, "last_modified_t"),
                Completeness = JsonDataSanitizer.GetInt(root, "completeness", 0, 100)
                
                // RawJson removed to save disk space (~90% reduction)
            };
            
            // Validate that normalized barcode is valid
            if (string.IsNullOrWhiteSpace(product.NormalizedBarcode))
                return null;
                
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse product from JSON");
            return null;
        }
    }
}

