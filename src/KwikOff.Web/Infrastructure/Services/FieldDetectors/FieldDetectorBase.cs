using KwikOff.Web.Domain.Interfaces;

namespace KwikOff.Web.Infrastructure.Services.FieldDetectors;

/// <summary>
/// Base class for field detectors providing common utilities.
/// </summary>
public abstract class FieldDetectorBase : IFieldDetector
{
    public abstract string FieldName { get; }
    public abstract string DisplayName { get; }
    public virtual bool IsRequired => false;
    public virtual bool IsFsma204 => false;

    /// <summary>
    /// Header names that indicate an exact match.
    /// </summary>
    protected abstract IReadOnlyList<string> ExactMatches { get; }

    /// <summary>
    /// Partial header names that suggest a match.
    /// </summary>
    protected virtual IReadOnlyList<string> PartialMatches => Array.Empty<string>();

    public virtual FieldDetectionResult Detect(string columnName, IReadOnlyList<string> sampleValues)
    {
        var lowerName = columnName.ToLowerInvariant().Trim();
        var normalizedName = NormalizeColumnName(lowerName);

        // Check exact matches first (highest confidence)
        foreach (var exact in ExactMatches)
        {
            if (normalizedName == exact.ToLowerInvariant())
            {
                return new FieldDetectionResult(FieldName, DisplayName, 1.0,
                    $"Exact header match: '{exact}'", IsRequired, IsFsma204);
            }
        }

        // Check partial matches
        foreach (var partial in PartialMatches)
        {
            if (normalizedName.Contains(partial.ToLowerInvariant()))
            {
                return new FieldDetectionResult(FieldName, DisplayName, 0.85,
                    $"Header contains: '{partial}'", IsRequired, IsFsma204);
            }
        }

        // Check data pattern if no header match
        var patternConfidence = AnalyzeDataPattern(sampleValues);
        if (patternConfidence > 0)
        {
            return new FieldDetectionResult(FieldName, DisplayName, patternConfidence,
                "Data pattern match", IsRequired, IsFsma204);
        }

        return FieldDetectionResult.NoMatch(FieldName, DisplayName, IsRequired, IsFsma204);
    }

    /// <summary>
    /// Analyzes sample data values to detect patterns.
    /// Override in subclasses for specific pattern detection.
    /// </summary>
    protected virtual double AnalyzeDataPattern(IReadOnlyList<string> sampleValues) => 0.0;

    /// <summary>
    /// Normalizes a column name by removing common separators and spaces.
    /// </summary>
    protected static string NormalizeColumnName(string name)
    {
        return name.Replace("_", "")
                   .Replace("-", "")
                   .Replace(" ", "")
                   .Replace(".", "")
                   .ToLowerInvariant();
    }

    /// <summary>
    /// Gets the percentage of non-empty values.
    /// </summary>
    protected static double GetNonEmptyRatio(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return 0;
        return values.Count(v => !string.IsNullOrWhiteSpace(v)) / (double)values.Count;
    }

    /// <summary>
    /// Gets the percentage of values matching a predicate.
    /// </summary>
    protected static double GetMatchingRatio(IReadOnlyList<string> values, Func<string, bool> predicate)
    {
        var nonEmpty = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (nonEmpty.Count == 0) return 0;
        return nonEmpty.Count(predicate) / (double)nonEmpty.Count;
    }
}
