namespace KwikOff.Web.Domain.ValueObjects;

/// <summary>
/// Represents a complete column mapping configuration for an import.
/// </summary>
public class ColumnMapping
{
    // Required fields
    public ColumnMapInfo? Barcode { get; set; }
    public ColumnMapInfo? ProductName { get; set; }

    // Recommended fields
    public ColumnMapInfo? Brand { get; set; }
    public ColumnMapInfo? Category { get; set; }

    // Optional fields
    public ColumnMapInfo? Description { get; set; }
    public ColumnMapInfo? Price { get; set; }
    public ColumnMapInfo? SalesPrice { get; set; }
    public ColumnMapInfo? InternalSku { get; set; }
    public ColumnMapInfo? Supplier { get; set; }
    public ColumnMapInfo? Allergens { get; set; }
    public ColumnMapInfo? Quantity { get; set; }
    public ColumnMapInfo? UnitOfMeasure { get; set; }

    // FSMA 204 Compliance fields
    public ColumnMapInfo? TraceabilityLotCode { get; set; }
    public ColumnMapInfo? OriginLocation { get; set; }
    public ColumnMapInfo? CurrentLocation { get; set; }
    public ColumnMapInfo? DestinationLocation { get; set; }
    public ColumnMapInfo? HarvestDate { get; set; }
    public ColumnMapInfo? PackDate { get; set; }
    public ColumnMapInfo? ShipDate { get; set; }
    public ColumnMapInfo? ReceiveDate { get; set; }
    public ColumnMapInfo? ExpirationDate { get; set; }
    public ColumnMapInfo? ReferenceDocumentType { get; set; }
    public ColumnMapInfo? ReferenceDocumentNumber { get; set; }

    /// <summary>
    /// Checks if all required fields are mapped.
    /// </summary>
    public bool HasRequiredFields() =>
        Barcode?.ColumnIndex >= 0 && ProductName?.ColumnIndex >= 0;
}

/// <summary>
/// Information about a single column mapping.
/// </summary>
public class ColumnMapInfo
{
    /// <summary>
    /// The column index in the source file (0-based).
    /// </summary>
    public int ColumnIndex { get; set; } = -1;

    /// <summary>
    /// The original column name from the source file.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for this mapping (0.0-1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reason for this mapping (e.g., "Exact header match").
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether this mapping was set manually by user.
    /// </summary>
    public bool IsManual { get; set; }
}
