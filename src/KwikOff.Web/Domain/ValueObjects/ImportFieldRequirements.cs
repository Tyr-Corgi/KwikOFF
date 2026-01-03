namespace KwikOff.Web.Domain.ValueObjects;

/// <summary>
/// Defines field requirements for product imports.
/// Centralizes business rules about required vs optional fields.
/// </summary>
public static class ImportFieldRequirements
{
    /// <summary>
    /// Fields that are absolutely required for import to proceed.
    /// </summary>
    public static readonly IReadOnlyList<FieldRequirement> RequiredFields = new List<FieldRequirement>
    {
        new("Barcode", "Barcode/SKU/UPC/EAN", true, false),
        new("ProductName", "Product Name/Item Name", true, false),
    };

    /// <summary>
    /// Fields that are recommended but not strictly required.
    /// </summary>
    public static readonly IReadOnlyList<FieldRequirement> RecommendedFields = new List<FieldRequirement>
    {
        new("Brand", "Brand/Manufacturer", false, false),
        new("Category", "Category", false, false),
    };

    /// <summary>
    /// Optional fields that can enhance product data.
    /// </summary>
    public static readonly IReadOnlyList<FieldRequirement> OptionalFields = new List<FieldRequirement>
    {
        new("Description", "Description", false, false),
        new("Price", "Price", false, false),
        new("SalesPrice", "Sale Price", false, false),
        new("InternalSku", "Internal SKU", false, false),
        new("Supplier", "Supplier", false, false),
        new("Allergens", "Allergens", false, false),
        new("Quantity", "Quantity", false, false),
        new("UnitOfMeasure", "Unit of Measure", false, false),
    };

    /// <summary>
    /// FSMA 204 compliance fields for FDA food traceability.
    /// These are required for compliance but won't block import.
    /// </summary>
    public static readonly IReadOnlyList<FieldRequirement> Fsma204Fields = new List<FieldRequirement>
    {
        new("TraceabilityLotCode", "Traceability Lot Code (TLC)", false, true),
        new("OriginLocation", "Origin Location", false, true),
        new("CurrentLocation", "Current Location", false, true),
        new("DestinationLocation", "Destination Location", false, true),
        new("HarvestDate", "Harvest Date", false, true),
        new("PackDate", "Pack Date", false, true),
        new("ShipDate", "Ship Date", false, true),
        new("ReceiveDate", "Receive Date", false, true),
        new("ExpirationDate", "Expiration Date", false, true),
        new("ReferenceDocumentType", "Reference Document Type", false, true),
        new("ReferenceDocumentNumber", "Reference Document Number", false, true),
    };

    /// <summary>
    /// All field requirements combined.
    /// </summary>
    public static IReadOnlyList<FieldRequirement> AllFields =>
        RequiredFields
            .Concat(RecommendedFields)
            .Concat(OptionalFields)
            .Concat(Fsma204Fields)
            .ToList();

    /// <summary>
    /// Get requirement for a specific field name.
    /// </summary>
    public static FieldRequirement? GetFieldRequirement(string fieldName) =>
        AllFields.FirstOrDefault(f => f.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Represents a single field requirement.
/// </summary>
public record FieldRequirement(
    string FieldName,
    string DisplayName,
    bool IsRequired,
    bool IsFsma204);
