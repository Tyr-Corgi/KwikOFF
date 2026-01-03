namespace KwikOff.Web.Domain.Entities;

/// <summary>
/// Represents a product imported from a CSV/Excel file.
/// Includes FSMA 204 compliance fields for FDA food traceability.
/// </summary>
public class ImportedProduct
{
    public long Id { get; set; }

    // Required fields
    public string Barcode { get; set; } = string.Empty;
    public string NormalizedBarcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    // Optional but recommended fields
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Supplier { get; set; }
    public string? InternalSku { get; set; }

    // Pricing
    public decimal? Price { get; set; }
    public decimal? SalesPrice { get; set; }

    // Product details
    public string? Allergens { get; set; }
    public decimal? Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }

    // FSMA 204 Compliance Fields (FDA Food Traceability)
    public string? TraceabilityLotCode { get; set; }
    public string? OriginLocation { get; set; }
    public string? CurrentLocation { get; set; }
    public string? DestinationLocation { get; set; }
    public DateTime? HarvestDate { get; set; }
    public DateTime? PackDate { get; set; }
    public DateTime? ShipDate { get; set; }
    public DateTime? ReceiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? ReferenceDocumentType { get; set; }
    public string? ReferenceDocumentNumber { get; set; }

    // Import metadata
    public string? TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public string? FileName { get; set; }
    public int RowNumber { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Original data stored as JSONB for flexibility
    public string? OriginalData { get; set; }

    // Navigation properties
    public ICollection<ComparisonResult> ComparisonResults { get; set; } = new List<ComparisonResult>();
}
