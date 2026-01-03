using System.Text.RegularExpressions;
using KwikOff.Web.Domain.Interfaces;

namespace KwikOff.Web.Infrastructure.Services.FieldDetectors;

/// <summary>
/// Detects barcode/SKU/UPC/EAN columns.
/// </summary>
public class BarcodeFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Barcode";
    public override string DisplayName => "Barcode/SKU";
    public override bool IsRequired => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "barcode", "sku", "upc", "ean", "gtin", "itemcode", "productcode",
        "item_code", "product_code", "upc_code", "ean_code", "barcode_number"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "barcode", "sku", "upc", "ean", "gtin"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Barcodes are typically 8-14 digit numbers
        var barcodePattern = new Regex(@"^[0-9]{8,14}$");
        var matchRatio = GetMatchingRatio(sampleValues, v => barcodePattern.IsMatch(v.Trim()));

        if (matchRatio >= 0.95) return 0.95;
        if (matchRatio >= 0.80) return 0.80;
        if (matchRatio >= 0.50) return 0.60;
        return 0;
    }
}

/// <summary>
/// Detects product name columns.
/// </summary>
public class ProductNameFieldDetector : FieldDetectorBase
{
    public override string FieldName => "ProductName";
    public override string DisplayName => "Product Name";
    public override bool IsRequired => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "productname", "product_name", "itemname", "item_name", "name",
        "product", "item", "description", "product_description", "title"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "productname", "itemname", "prodname"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Product names are typically strings with average length 10-100 chars
        var validValues = sampleValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (validValues.Count == 0) return 0;

        var avgLength = validValues.Average(v => v.Length);
        var hasSpaces = GetMatchingRatio(sampleValues, v => v.Contains(' '));

        // Product names typically have spaces and reasonable length
        if (avgLength >= 10 && avgLength <= 100 && hasSpaces >= 0.5)
            return 0.5; // Low confidence for pattern-only detection

        return 0;
    }
}

/// <summary>
/// Detects brand/manufacturer columns.
/// </summary>
public class BrandFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Brand";
    public override string DisplayName => "Brand/Manufacturer";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "brand", "brands", "manufacturer", "mfr", "vendor", "maker",
        "brand_name", "manufacturer_name", "company"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "brand", "manufacturer", "vendor"
    };
}

/// <summary>
/// Detects category columns.
/// </summary>
public class CategoryFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Category";
    public override string DisplayName => "Category";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "category", "categories", "productcategory", "product_category",
        "itemcategory", "item_category", "type", "product_type", "class"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "category", "categor"
    };
}

/// <summary>
/// Detects description columns.
/// </summary>
public class DescriptionFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Description";
    public override string DisplayName => "Description";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "description", "desc", "product_description", "item_description",
        "long_description", "full_description", "details"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "description", "desc"
    };
}

/// <summary>
/// Detects price columns.
/// </summary>
public class PriceFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Price";
    public override string DisplayName => "Price";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "price", "unitprice", "unit_price", "cost", "retail_price",
        "regular_price", "list_price", "msrp"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "price", "cost"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Prices are typically decimal numbers, possibly with currency symbols
        var pricePattern = new Regex(@"^[\$\€\£]?\s*[0-9]+\.?[0-9]*$");
        var matchRatio = GetMatchingRatio(sampleValues, v =>
            pricePattern.IsMatch(v.Trim().Replace(",", "")));

        if (matchRatio >= 0.90) return 0.85;
        if (matchRatio >= 0.70) return 0.65;
        return 0;
    }
}

/// <summary>
/// Detects sale price columns.
/// </summary>
public class SalesPriceFieldDetector : FieldDetectorBase
{
    public override string FieldName => "SalesPrice";
    public override string DisplayName => "Sale Price";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "saleprice", "sale_price", "salesprice", "sales_price",
        "discounted_price", "promo_price", "special_price"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "sale", "discount", "promo", "special"
    };
}

/// <summary>
/// Detects internal SKU columns.
/// </summary>
public class InternalSkuFieldDetector : FieldDetectorBase
{
    public override string FieldName => "InternalSku";
    public override string DisplayName => "Internal SKU";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "internalsku", "internal_sku", "itemid", "item_id",
        "productid", "product_id", "plu", "item_number"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "internal", "itemid", "productid"
    };
}

/// <summary>
/// Detects supplier columns.
/// </summary>
public class SupplierFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Supplier";
    public override string DisplayName => "Supplier";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "supplier", "vendor", "supplier_name", "vendor_name",
        "distributor", "wholesaler"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "supplier", "vendor", "distributor"
    };
}

/// <summary>
/// Detects allergen columns.
/// </summary>
public class AllergensFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Allergens";
    public override string DisplayName => "Allergens";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "allergens", "allergen", "allergen_info", "allergen_warning",
        "contains", "may_contain", "allergy_info"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "allergen", "allergy"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Allergen data often contains common allergen keywords
        var allergenKeywords = new[] { "milk", "egg", "peanut", "tree nut", "wheat", "soy", "fish", "shellfish", "gluten" };
        var matchRatio = GetMatchingRatio(sampleValues, v =>
            allergenKeywords.Any(a => v.ToLowerInvariant().Contains(a)));

        if (matchRatio >= 0.30) return 0.75;
        return 0;
    }
}

/// <summary>
/// Detects quantity columns.
/// </summary>
public class QuantityFieldDetector : FieldDetectorBase
{
    public override string FieldName => "Quantity";
    public override string DisplayName => "Quantity";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "quantity", "qty", "amount", "count", "units",
        "stock", "inventory", "onhand", "on_hand"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "quantity", "qty"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Quantities are typically positive integers or decimals
        var numericPattern = new Regex(@"^[0-9]+\.?[0-9]*$");
        var matchRatio = GetMatchingRatio(sampleValues, v => numericPattern.IsMatch(v.Trim()));

        if (matchRatio >= 0.95) return 0.6;
        return 0;
    }
}

/// <summary>
/// Detects unit of measure columns.
/// </summary>
public class UnitOfMeasureFieldDetector : FieldDetectorBase
{
    public override string FieldName => "UnitOfMeasure";
    public override string DisplayName => "Unit of Measure";

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "uom", "unit", "unitofmeasure", "unit_of_measure",
        "units", "measure", "packaging_unit"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "unit", "uom", "measure"
    };

    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // UoM values are typically short strings like "ea", "lb", "oz", "kg"
        var uomKeywords = new[] { "ea", "each", "lb", "oz", "kg", "g", "ml", "l", "ct", "pk", "cs", "case", "box" };
        var matchRatio = GetMatchingRatio(sampleValues, v =>
            uomKeywords.Any(u => v.Trim().ToLowerInvariant() == u ||
                                  v.Trim().ToLowerInvariant().EndsWith(" " + u)));

        if (matchRatio >= 0.50) return 0.80;
        return 0;
    }
}
