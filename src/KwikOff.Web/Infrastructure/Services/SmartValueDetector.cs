using System.Text.RegularExpressions;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Detects barcodes, product names, sizes, etc. by analyzing CONTENT, not column names.
/// Handles messy client data where values are in the wrong columns.
/// </summary>
public interface ISmartValueDetector
{
    /// <summary>
    /// Analyze a row and find the best barcode and product name, regardless of column mapping.
    /// </summary>
    SmartRowResult AnalyzeRow(List<string> values, string? mappedBarcode, string? mappedProductName);
}

public class SmartRowResult
{
    public string Barcode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Size { get; set; } = "";
    public bool WasFixed { get; set; }
}

public partial class SmartValueDetector : ISmartValueDetector
{
    // Regex patterns
    [GeneratedRegex(@"^\d{8,14}$")]
    private static partial Regex BarcodePattern();
    
    [GeneratedRegex(@"^(\d{8,14})[|/\-\s]")]
    private static partial Regex BarcodeWithJunkPattern();
    
    [GeneratedRegex(@"^\d{1,7}$")]
    private static partial Regex ShortSkuPattern();
    
    [GeneratedRegex(@"^[\d.]+\s*(oz|fl\s*oz|lb|lbs|ct|pk|pack|ml|l|g|kg|gal|pt|qt|ea|each|per\s*lb|count)s?\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();
    
    [GeneratedRegex(@"^\d{1,3}\.\d{2}$")]
    private static partial Regex PricePattern();
    
    [GeneratedRegex(@"^\d{3}\s+[A-Z]", RegexOptions.IgnoreCase)]
    private static partial Regex DepartmentPattern();

    private static readonly HashSet<string> BooleanValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "no", "true", "false", "y", "n"
    };

    private static readonly HashSet<string> UnitOnlyValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "ea", "each", "lb", "oz", "ct", "pk", "per lb", "per", "lbs"
    };

    // Header row values that indicate this row should be skipped entirely
    private static readonly HashSet<string> HeaderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "item id", "item name", "product name", "barcode", "upc", "sku", "department",
        "category", "brand", "price", "description", "size", "unit"
    };

    public SmartRowResult AnalyzeRow(List<string> values, string? mappedBarcode, string? mappedProductName)
    {
        var result = new SmartRowResult
        {
            Barcode = mappedBarcode?.Trim() ?? "",
            ProductName = mappedProductName?.Trim() ?? "",
            WasFixed = false
        };

        // Check if this is a header row (duplicate headers in middle of data)
        if (IsHeaderRow(result.Barcode, result.ProductName))
        {
            // Return empty values so this row gets skipped
            result.Barcode = "";
            result.ProductName = "";
            return result;
        }

        // Check if mapped values are valid
        bool barcodeValid = IsValidBarcode(result.Barcode);
        bool productNameValid = IsValidProductName(result.ProductName);

        // If both are valid, no fixing needed
        if (barcodeValid && productNameValid)
        {
            ExtractBrandFromName(result);
            return result;
        }

        // Scan all values to find what we need
        var candidates = ClassifyAllValues(values);

        // Fix barcode if invalid
        if (!barcodeValid)
        {
            var bestBarcode = FindBestBarcode(candidates);
            if (!string.IsNullOrEmpty(bestBarcode))
            {
                result.Barcode = bestBarcode;
                result.WasFixed = true;
            }
        }

        // Fix product name if invalid
        if (!productNameValid)
        {
            var bestName = FindBestProductName(candidates);
            if (!string.IsNullOrEmpty(bestName))
            {
                result.ProductName = bestName;
                result.WasFixed = true;
            }
            else
            {
                // IMPORTANT: Clear the invalid product name - don't keep garbage like "NO", "YES"
                result.ProductName = "";
            }
        }

        // Find size if available
        result.Size = FindSize(candidates);
        
        // Extract brand from product name
        ExtractBrandFromName(result);

        return result;
    }

    private static bool IsHeaderRow(string barcode, string productName)
    {
        // Check if either value is a common header column name
        return HeaderValues.Contains(barcode) || HeaderValues.Contains(productName);
    }

    private static bool IsValidBarcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        
        // Valid if it's 1-14 digits (short SKU or full barcode)
        return trimmed.All(char.IsDigit) && trimmed.Length >= 1 && trimmed.Length <= 14;
    }

    private static bool IsValidProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        
        // Invalid if too short
        if (trimmed.Length < 3)
            return false;

        // Invalid if it's just a barcode
        if (BarcodePattern().IsMatch(trimmed))
            return false;

        // Invalid if it's a boolean
        if (BooleanValues.Contains(trimmed))
            return false;

        // Invalid if it's just a unit
        if (UnitOnlyValues.Contains(trimmed))
            return false;

        // Invalid if it's a price
        if (PricePattern().IsMatch(trimmed))
            return false;

        // Invalid if it's a size
        if (SizePattern().IsMatch(trimmed))
            return false;

        // Must have at least some letters
        return trimmed.Any(char.IsLetter);
    }

    private static Dictionary<string, List<string>> ClassifyAllValues(List<string> values)
    {
        var classified = new Dictionary<string, List<string>>
        {
            ["barcode"] = new(),
            ["sku"] = new(),
            ["product_name"] = new(),
            ["size"] = new(),
            ["price"] = new(),
            ["department"] = new(),
            ["junk"] = new()
        };

        // Skip first column (index 0) - it's almost always Row Number
        for (int i = 1; i < values.Count; i++)
        {
            var trimmed = values[i]?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var type = ClassifyValue(trimmed);
            if (classified.ContainsKey(type))
            {
                classified[type].Add(trimmed);
            }
        }

        return classified;
    }

    private static string ClassifyValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";

        var v = value.Trim();
        var vLower = v.ToLowerInvariant();

        // Boolean
        if (BooleanValues.Contains(vLower))
            return "boolean";

        // Unit only
        if (UnitOnlyValues.Contains(vLower))
            return "unit";

        // Price (check before barcode since prices have decimals)
        if (PricePattern().IsMatch(v))
            return "price";

        // Barcode (8-14 digits)
        if (BarcodePattern().IsMatch(v))
            return "barcode";

        // Barcode with junk
        if (BarcodeWithJunkPattern().IsMatch(v))
            return "barcode_dirty";

        // Short SKU (1-7 digits)
        if (ShortSkuPattern().IsMatch(v))
            return "sku";

        // Size
        if (SizePattern().IsMatch(v))
            return "size";

        // Department
        if (DepartmentPattern().IsMatch(v))
            return "department";

        // Product name (has letters, reasonably long)
        if (v.Any(char.IsLetter))
        {
            if (v.Length < 3)
                return "junk";
            return "product_name";
        }

        return "junk";
    }

    private static string FindBestBarcode(Dictionary<string, List<string>> candidates)
    {
        // ONLY use full barcodes (8-14 digits) - these are real UPC/EAN codes
        if (candidates["barcode"].Count > 0)
        {
            // Prefer longer barcodes (13-digit EAN over 8-digit UPC)
            return candidates["barcode"].OrderByDescending(b => b.Length).First();
        }

        // Only fall back to SKU if absolutely no real barcode found
        // AND the SKU is at least 4 digits (to avoid row numbers like 1, 2, 3...)
        if (candidates["sku"].Count > 0)
        {
            var validSkus = candidates["sku"].Where(s => s.Length >= 4).ToList();
            if (validSkus.Count > 0)
            {
                return validSkus.OrderByDescending(s => s.Length).First();
            }
        }

        return "";
    }

    private static string FindBestProductName(Dictionary<string, List<string>> candidates)
    {
        var names = candidates["product_name"];
        if (names.Count == 0)
            return "";

        if (names.Count == 1)
            return CleanProductName(names[0]);

        // Score each candidate - prefer longer, properly capitalized names
        var scored = names
            .Select(n => (Name: n, Score: ScoreProductName(n)))
            .OrderByDescending(x => x.Score)
            .ToList();

        return CleanProductName(scored[0].Name);
    }

    private static int ScoreProductName(string name)
    {
        int score = name.Length;

        // Bonus for capitalized words
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        score += words.Count(w => w.Length > 0 && char.IsUpper(w[0])) * 5;

        // Bonus for multiple words
        score += words.Length * 3;

        // Penalty for fragments (partial words at start)
        if (name.Length > 2 && char.IsLower(name[0]))
            score -= 10;

        // Penalty for abbreviated/partial names
        var partialPrefixes = new[] { "rr's", "ck ", "te ", "s ", "e ", "y ", "orn " };
        if (partialPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            score -= 15;

        return score;
    }

    private static string CleanProductName(string name)
    {
        // Remove encoding artifacts
        return name
            .Replace("��", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static string FindSize(Dictionary<string, List<string>> candidates)
    {
        if (candidates["size"].Count > 0)
            return candidates["size"][0];
        return "";
    }

    private static void ExtractBrandFromName(SmartRowResult result)
    {
        if (string.IsNullOrEmpty(result.ProductName))
            return;

        // Known multi-word brands
        var knownBrands = new[]
        {
            "Stonewall Kitchen", "Cracker Barrel", "Back East", "Shaggy Coo", "Shaggy Coos",
            "Heaven Earth", "Other Half", "Mount Holly", "Frost Beer", "Dr Pepper",
            "Coca Cola", "Pepsi Cola", "Ben & Jerry", "Haagen Dazs", "Blue Moon",
            "Justin's", "Luna", "Bovetti", "Colgate", "Pillsbury", "Planters", "Pampers"
        };

        var nameLower = result.ProductName.ToLowerInvariant();
        foreach (var brand in knownBrands)
        {
            if (nameLower.StartsWith(brand.ToLowerInvariant()))
            {
                result.Brand = brand;
                return;
            }
        }

        // Default: first capitalized word if not generic
        var genericWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "open", "misc", "assorted", "organic", "natural", "fresh"
        };

        var words = result.ProductName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0 && words[0].Length > 2 && char.IsUpper(words[0][0]))
        {
            if (!genericWords.Contains(words[0]))
            {
                result.Brand = words[0];
            }
        }
    }
}

