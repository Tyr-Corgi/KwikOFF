namespace KwikOff.Web.Domain.Interfaces;

/// <summary>
/// Interface for field detectors following the Strategy pattern.
/// Each detector is responsible for detecting one type of field in import data.
/// </summary>
public interface IFieldDetector
{
    /// <summary>
    /// The internal field name (e.g., "Barcode", "ProductName").
    /// </summary>
    string FieldName { get; }

    /// <summary>
    /// Human-readable display name for the field.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this field is required for import.
    /// </summary>
    bool IsRequired { get; }

    /// <summary>
    /// Whether this field is part of FSMA 204 compliance.
    /// </summary>
    bool IsFsma204 { get; }

    /// <summary>
    /// Analyzes a column to determine if it matches this field.
    /// </summary>
    /// <param name="columnName">The column header name from the file.</param>
    /// <param name="sampleValues">Sample values from the column (first 100 rows).</param>
    /// <returns>Detection result with confidence score and reasoning.</returns>
    FieldDetectionResult Detect(string columnName, IReadOnlyList<string> sampleValues);
}

/// <summary>
/// Result of a field detection analysis.
/// </summary>
public record FieldDetectionResult(
    string FieldName,
    string DisplayName,
    double Confidence,
    string Reason,
    bool IsRequired,
    bool IsFsma204)
{
    /// <summary>
    /// Creates a result indicating no match.
    /// </summary>
    public static FieldDetectionResult NoMatch(string fieldName, string displayName, bool isRequired, bool isFsma204) =>
        new(fieldName, displayName, 0.0, "No match", isRequired, isFsma204);
}
