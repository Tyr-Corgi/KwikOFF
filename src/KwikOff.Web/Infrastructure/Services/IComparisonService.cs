using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Domain.Enums;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for comparing imported products against Open Food Facts database.
/// </summary>
public interface IComparisonService
{
    /// <summary>
    /// Compares a single imported product against Open Food Facts.
    /// </summary>
    Task<ComparisonResult> CompareProductAsync(ImportedProduct importedProduct, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares all imported products in a batch.
    /// </summary>
    Task<List<ComparisonResult>> CompareBatchAsync(
        Guid batchId, 
        string tenantId, 
        int maxProducts = 500, 
        string? searchFilter = null, 
        CancellationToken cancellationToken = default);
}
