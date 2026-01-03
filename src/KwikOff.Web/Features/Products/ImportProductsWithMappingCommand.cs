using KwikOff.Web.Domain.ValueObjects;
using MediatR;

namespace KwikOff.Web.Features.Products;

/// <summary>
/// Command to import products with a specific column mapping.
/// </summary>
public record ImportProductsWithMappingCommand(
    Stream FileStream,
    string FileName,
    string TenantId,
    ColumnMapping Mapping,
    bool SaveMapping = false,
    string? UserId = null
) : IRequest<ImportProductsResult>;

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportProductsResult
{
    public bool Success { get; set; }
    public Guid BatchId { get; set; }
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan Duration { get; set; }
}
