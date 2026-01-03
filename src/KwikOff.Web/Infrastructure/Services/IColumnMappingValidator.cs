using KwikOff.Web.Domain.ValueObjects;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Validates column mappings against business rules.
/// </summary>
public interface IColumnMappingValidator
{
    /// <summary>
    /// Validates a column mapping configuration.
    /// </summary>
    ValidationResult Validate(ColumnMapping mapping);
}

/// <summary>
/// Result of column mapping validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Default implementation of column mapping validator.
/// </summary>
public class ColumnMappingValidator : IColumnMappingValidator
{
    public ValidationResult Validate(ColumnMapping mapping)
    {
        var result = new ValidationResult();

        // Check required fields
        if (mapping.Barcode == null || mapping.Barcode.ColumnIndex < 0)
        {
            result.Errors.Add("Barcode/SKU field is required and must be mapped");
        }

        if (mapping.ProductName == null || mapping.ProductName.ColumnIndex < 0)
        {
            result.Errors.Add("Product Name field is required and must be mapped");
        }

        // Warn about low confidence mappings
        var lowConfidenceFields = new List<string>();
        CheckConfidence(mapping.Barcode, "Barcode", lowConfidenceFields);
        CheckConfidence(mapping.ProductName, "Product Name", lowConfidenceFields);
        CheckConfidence(mapping.Brand, "Brand", lowConfidenceFields);

        if (lowConfidenceFields.Count > 0)
        {
            result.Warnings.Add($"Low confidence mappings detected for: {string.Join(", ", lowConfidenceFields)}");
        }

        // Warn about missing recommended fields
        if (mapping.Brand == null || mapping.Brand.ColumnIndex < 0)
        {
            result.Warnings.Add("Brand field is recommended but not mapped");
        }

        // Warn about missing FSMA 204 fields
        var missingFsma = new List<string>();
        if (mapping.TraceabilityLotCode == null) missingFsma.Add("Traceability Lot Code");
        if (mapping.OriginLocation == null) missingFsma.Add("Origin Location");
        if (mapping.HarvestDate == null) missingFsma.Add("Harvest Date");
        if (mapping.PackDate == null) missingFsma.Add("Pack Date");
        if (mapping.ShipDate == null) missingFsma.Add("Ship Date");
        if (mapping.ReceiveDate == null) missingFsma.Add("Receive Date");

        if (missingFsma.Count > 0)
        {
            result.Warnings.Add($"FSMA 204 fields not mapped (may affect compliance): {string.Join(", ", missingFsma)}");
        }

        return result;
    }

    private static void CheckConfidence(ColumnMapInfo? mapInfo, string fieldName, List<string> lowConfidenceList)
    {
        if (mapInfo != null && mapInfo.ColumnIndex >= 0 && mapInfo.Confidence < 0.9)
        {
            lowConfidenceList.Add($"{fieldName} ({mapInfo.Confidence:P0})");
        }
    }
}
