using KwikOff.Web.Domain.Enums;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for exporting data to CSV.
/// </summary>
public interface ICsvExporter
{
    /// <summary>
    /// Exports matched products to CSV.
    /// </summary>
    Task<byte[]> ExportMatchedProductsAsync(
        string tenantId,
        Guid? batchId = null,
        MatchStatus? status = null,
        IEnumerable<string>? fields = null,
        bool includeImages = false,
        CancellationToken cancellationToken = default);
}
