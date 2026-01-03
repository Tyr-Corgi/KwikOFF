using KwikOff.Web.Domain.ValueObjects;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for detecting and mapping columns in imported files.
/// </summary>
public interface IColumnDetectionService
{
    /// <summary>
    /// Analyzes a file to detect column mappings.
    /// </summary>
    /// <param name="fileStream">The file stream to analyze.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>Detection result with suggested mappings.</returns>
    Task<ColumnDetectionResult> DetectColumnsAsync(Stream fileStream, string fileName, string tenantId);

    /// <summary>
    /// Saves a column mapping configuration for a tenant.
    /// </summary>
    Task SaveColumnMappingAsync(string tenantId, string filePattern, ColumnMapping mapping, string? userId);

    /// <summary>
    /// Gets a saved column mapping for a tenant and file pattern.
    /// </summary>
    Task<ColumnMapping?> GetSavedMappingAsync(string tenantId, string fileName);
}

/// <summary>
/// Result of column detection analysis.
/// </summary>
public class ColumnDetectionResult
{
    /// <summary>
    /// The suggested column mapping.
    /// </summary>
    public ColumnMapping DetectedMapping { get; set; } = new();

    /// <summary>
    /// All detected columns with their suggestions.
    /// </summary>
    public List<DetectedColumn> AllColumns { get; set; } = new();

    /// <summary>
    /// Overall confidence score (0.0-1.0).
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Whether manual review is recommended.
    /// </summary>
    public bool RequiresManualReview => OverallConfidence < 1.0 || !HasRequiredFields;

    /// <summary>
    /// Whether all required fields are mapped.
    /// </summary>
    public bool HasRequiredFields => DetectedMapping.HasRequiredFields();

    /// <summary>
    /// Validation errors (missing required fields).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings (missing optional/FSMA fields).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Sample data from the file (first 5 rows).
    /// </summary>
    public List<Dictionary<string, string>> SampleData { get; set; } = new();

    /// <summary>
    /// Column headers from the file.
    /// </summary>
    public List<string> Headers { get; set; } = new();
}

/// <summary>
/// Information about a detected column.
/// </summary>
public class DetectedColumn
{
    /// <summary>
    /// The column index (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The original column header name.
    /// </summary>
    public string HeaderName { get; set; } = string.Empty;

    /// <summary>
    /// Sample values from this column.
    /// </summary>
    public List<string> SampleValues { get; set; } = new();

    /// <summary>
    /// Ranked field suggestions for this column.
    /// </summary>
    public List<FieldSuggestion> Suggestions { get; set; } = new();

    /// <summary>
    /// The best matching field name, if any.
    /// </summary>
    public string? BestMatch => Suggestions.FirstOrDefault()?.FieldName;

    /// <summary>
    /// The confidence of the best match.
    /// </summary>
    public double BestConfidence => Suggestions.FirstOrDefault()?.Confidence ?? 0;
}

/// <summary>
/// A field suggestion for a column.
/// </summary>
public class FieldSuggestion
{
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsFsma204 { get; set; }
}
