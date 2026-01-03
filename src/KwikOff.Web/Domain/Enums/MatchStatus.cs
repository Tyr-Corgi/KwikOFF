namespace KwikOff.Web.Domain.Enums;

/// <summary>
/// Represents the result status of a product comparison.
/// </summary>
public enum MatchStatus
{
    /// <summary>
    /// Product not yet compared.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Product found in Open Food Facts with matching data.
    /// </summary>
    Matched = 1,

    /// <summary>
    /// Product not found in Open Food Facts database.
    /// </summary>
    Unmatched = 2,

    /// <summary>
    /// Product found but with data discrepancies.
    /// </summary>
    Discrepancy = 3,

    /// <summary>
    /// Multiple potential matches found.
    /// </summary>
    MultipleMatches = 4,

    /// <summary>
    /// Comparison failed due to an error.
    /// </summary>
    Error = 5
}
