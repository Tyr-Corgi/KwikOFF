using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Services.DataSanitizers;

namespace KwikOff.Web.Infrastructure.Services.Parsers;

/// <summary>
/// Parses Open Food Facts CSV data into domain entities.
/// The CSV export is tab-delimited and includes image URLs.
/// </summary>
public class OpenFoodFactsCsvParser
{
    private readonly IBarcodeNormalizer _barcodeNormalizer;
    private readonly ILogger<OpenFoodFactsCsvParser> _logger;
    private static readonly Regex ControlCharsRegex = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);

    public OpenFoodFactsCsvParser(IBarcodeNormalizer barcodeNormalizer, ILogger<OpenFoodFactsCsvParser> logger)
    {
        _barcodeNormalizer = barcodeNormalizer;
        _logger = logger;
    }

    /// <summary>
    /// Parses CSV data stream and yields OpenFoodFactsProduct entities.
    /// </summary>
    public IEnumerable<OpenFoodFactsProduct> ParseCsvStream(Stream stream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t", // OFF CSV is tab-delimited
            BadDataFound = null, // Ignore bad data
            MissingFieldFound = null, // Ignore missing fields
            HeaderValidated = null, // Don't validate headers
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var product = ParseCsvRow(csv);
            if (product != null)
            {
                yield return product;
            }
        }
    }

    /// <summary>
    /// Parses a single CSV row into an OpenFoodFactsProduct.
    /// Returns null if the product is invalid.
    /// </summary>
    private OpenFoodFactsProduct? ParseCsvRow(CsvReader csv)
    {
        try
        {
            var barcode = GetField(csv, "code");
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length > 50)
                return null;

            var product = new OpenFoodFactsProduct
            {
                Barcode = barcode,
                NormalizedBarcode = _barcodeNormalizer.Normalize(barcode),

                // Basic product info
                ProductName = GetField(csv, "product_name"),
                GenericName = GetField(csv, "generic_name"),
                Brands = GetField(csv, "brands"),
                Categories = GetField(csv, "categories"),
                CategoriesTags = GetField(csv, "categories_tags"),

                // Ingredients and allergens
                IngredientsText = GetField(csv, "ingredients_text"),
                Allergens = GetField(csv, "allergens"),
                AllergensTags = GetField(csv, "allergens_tags"),
                Traces = GetField(csv, "traces"),
                TracesTags = GetField(csv, "traces_tags"),

                // Nutrition scores
                NutritionGrades = GetField(csv, "nutrition_grades"),
                NovaGroup = GetFieldAsString(csv, "nova_groups"),
                Ecoscore = GetField(csv, "ecoscore_grade"),

                // Nutritional values (per 100g)
                EnergyKcal100g = GetFieldAsDecimal(csv, "energy-kcal_100g"),
                Fat100g = GetFieldAsDecimal(csv, "fat_100g"),
                SaturatedFat100g = GetFieldAsDecimal(csv, "saturated-fat_100g"),
                Carbohydrates100g = GetFieldAsDecimal(csv, "carbohydrates_100g"),
                Sugars100g = GetFieldAsDecimal(csv, "sugars_100g"),
                Fiber100g = GetFieldAsDecimal(csv, "fiber_100g"),
                Proteins100g = GetFieldAsDecimal(csv, "proteins_100g"),
                Salt100g = GetFieldAsDecimal(csv, "salt_100g"),
                Sodium100g = GetFieldAsDecimal(csv, "sodium_100g"),

                // Serving information
                ServingSize = GetField(csv, "serving_size"),
                ServingQuantity = GetFieldAsDecimal(csv, "serving_quantity"),

                // Labels and distribution
                Labels = GetField(csv, "labels"),
                LabelsTags = GetField(csv, "labels_tags"),
                Stores = GetField(csv, "stores"),
                Countries = GetField(csv, "countries"),
                CountriesTags = GetField(csv, "countries_tags"),

                // Images - THIS IS THE KEY PART!
                ImageUrl = GetField(csv, "image_url"),
                ImageSmallUrl = GetField(csv, "image_small_url"),
                ImageFrontUrl = GetField(csv, "image_front_url"),
                ImageIngredientsUrl = GetField(csv, "image_ingredients_url"),
                ImageNutritionUrl = GetField(csv, "image_nutrition_url"),

                // Packaging and quantity
                Packaging = GetField(csv, "packaging"),
                PackagingTags = GetField(csv, "packaging_tags"),
                Quantity = GetField(csv, "quantity"),

                // Origin information
                Origins = GetField(csv, "origins"),
                OriginsTags = GetField(csv, "origins_tags"),
                ManufacturingPlaces = GetField(csv, "manufacturing_places"),

                // Metadata
                Creator = GetField(csv, "creator", 500),
                LastModifiedBy = GetField(csv, "last_modified_by", 500),
                CreatedAt = GetFieldAsDateTime(csv, "created_t"),
                LastModified = GetFieldAsDateTime(csv, "last_modified_t"),
                Completeness = GetFieldAsInt(csv, "completeness")
            };

            // Validate that normalized barcode is valid
            if (string.IsNullOrWhiteSpace(product.NormalizedBarcode))
                return null;

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse CSV row");
            return null;
        }
    }

    private static string? GetField(CsvReader csv, string fieldName, int? maxLength = 10000)
    {
        try
        {
            var value = csv.GetField<string?>(fieldName);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Sanitize crowdsourced data (same as JsonDataSanitizer):
            // 1. Remove null bytes (PostgreSQL doesn't allow them)
            value = value.Replace("\0", "");
            
            // 2. Remove control characters (0x01-0x1F) except newlines, tabs, carriage returns
            value = ControlCharsRegex.Replace(value, "");
            
            // 3. Trim whitespace
            value = value.Trim();
            
            // 4. Truncate if needed
            if (maxLength.HasValue && value.Length > maxLength.Value)
                value = value[..maxLength.Value];

            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static decimal? GetFieldAsDecimal(CsvReader csv, string fieldName)
    {
        try
        {
            var value = csv.GetField<string?>(fieldName);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                // Validate range
                if (result < -999999 || result > 999999)
                    return null;
                return result;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static int? GetFieldAsInt(CsvReader csv, string fieldName)
    {
        try
        {
            var value = csv.GetField<string?>(fieldName);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out var result))
                return result;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFieldAsString(CsvReader csv, string fieldName)
    {
        try
        {
            var value = csv.GetField<string?>(fieldName);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? GetFieldAsDateTime(CsvReader csv, string fieldName)
    {
        try
        {
            var value = csv.GetField<string?>(fieldName);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // OFF uses Unix timestamps
            if (long.TryParse(value, out var timestamp))
            {
                // Validate timestamp range (1970-2100)
                if (timestamp < 0 || timestamp > 4102444800)
                    return null;

                return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}

