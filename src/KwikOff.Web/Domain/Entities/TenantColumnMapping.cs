namespace KwikOff.Web.Domain.Entities;

/// <summary>
/// Stores column mappings per tenant for persistent import configurations.
/// Remembers how columns were mapped for future imports.
/// </summary>
public class TenantColumnMapping
{
    public long Id { get; set; }

    /// <summary>
    /// Unique identifier for the tenant (company/organization).
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// File pattern this mapping applies to (e.g., "*.csv", "inventory*.xlsx").
    /// </summary>
    public string FilePattern { get; set; } = string.Empty;

    /// <summary>
    /// The column mapping configuration stored as JSONB.
    /// Contains ColumnMapping object serialized to JSON.
    /// </summary>
    public string ColumnMappingJson { get; set; } = string.Empty;

    /// <summary>
    /// User who created this mapping.
    /// </summary>
    public string? CreatedByUser { get; set; }

    /// <summary>
    /// When this mapping was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this mapping was last used for an import.
    /// </summary>
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this mapping has been used.
    /// </summary>
    public int UseCount { get; set; } = 0;

    /// <summary>
    /// Whether this mapping is active and can be auto-selected.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
