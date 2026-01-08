using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Domain.Enums;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KwikOff.Web.Features.Comparison;

/// <summary>
/// Command to compare products in a batch.
/// </summary>
public record CompareProductsCommand(
    Guid BatchId,
    string TenantId,
    int MaxProducts = 500,
    string? SearchFilter = null
) : IRequest<ComparisonSummary>;

/// <summary>
/// Summary of comparison results.
/// </summary>
public class ComparisonSummary
{
    public Guid ComparisonBatchId { get; set; }
    public int TotalProducts { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int DiscrepancyCount { get; set; }
    public int ErrorCount { get; set; }
    public double MatchRate => TotalProducts > 0 ? (double)MatchedCount / TotalProducts : 0;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Handler for CompareProductsCommand.
/// </summary>
public class CompareProductsHandler : IRequestHandler<CompareProductsCommand, ComparisonSummary>
{
    private readonly IComparisonService _comparisonService;
    private readonly ILogger<CompareProductsHandler> _logger;

    public CompareProductsHandler(
        IComparisonService comparisonService,
        ILogger<CompareProductsHandler> logger)
    {
        _comparisonService = comparisonService;
        _logger = logger;
    }

    public async Task<ComparisonSummary> Handle(
        CompareProductsCommand request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[HANDLER] CompareProductsHandler.Handle called for batch {request.BatchId}, MaxProducts: {request.MaxProducts}, SearchFilter: '{request.SearchFilter}'");
        var startTime = DateTime.UtcNow;

        try
        {
            Console.WriteLine("[HANDLER] Calling _comparisonService.CompareBatchAsync");
            var results = await _comparisonService.CompareBatchAsync(
                request.BatchId, request.TenantId, request.MaxProducts, request.SearchFilter, cancellationToken);
            Console.WriteLine($"[HANDLER] CompareBatchAsync returned {results.Count} results");

            var summary = new ComparisonSummary
            {
                ComparisonBatchId = results.FirstOrDefault()?.ComparisonBatchId ?? Guid.Empty,
                TotalProducts = results.Count,
                MatchedCount = results.Count(r => r.MatchStatus == MatchStatus.Matched),
                UnmatchedCount = results.Count(r => r.MatchStatus == MatchStatus.Unmatched),
                DiscrepancyCount = results.Count(r => r.MatchStatus == MatchStatus.Discrepancy),
                ErrorCount = results.Count(r => r.MatchStatus == MatchStatus.Error),
                Duration = DateTime.UtcNow - startTime
            };

            _logger.LogInformation(
                "Compared batch {BatchId}: {Matched} matched, {Unmatched} unmatched, {Discrepancy} discrepancies",
                request.BatchId, summary.MatchedCount, summary.UnmatchedCount, summary.DiscrepancyCount);

            Console.WriteLine($"[HANDLER] Returning summary: {summary.TotalProducts} products");
            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HANDLER] Exception in handler: {ex.Message}");
            Console.WriteLine($"[HANDLER] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

/// <summary>
/// Query to get comparison results.
/// </summary>
public record GetComparisonResultsQuery(
    string TenantId,
    Guid? ImportBatchId = null,
    MatchStatus? Status = null,
    int Page = 1,
    int PageSize = 10,
    string? SearchTerm = null
) : IRequest<ComparisonResultsPage>;

/// <summary>
/// Paged comparison results.
/// </summary>
public class ComparisonResultsPage
{
    public List<ComparisonResultDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// DTO for comparison result display.
/// </summary>
public class ComparisonResultDto
{
    public long Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public MatchStatus MatchStatus { get; set; }
    public double ConfidenceScore { get; set; }
    public string? OffProductName { get; set; }
    public string? OffBrand { get; set; }
    public string? OffBarcode { get; set; }
    public string? ImageUrl { get; set; }
    public bool HasDiscrepancies { get; set; }
    public DateTime ComparedAt { get; set; }
    
    // Secondary search tracking
    public bool UsedSecondarySearch { get; set; }
    public string? SecondarySearchMethod { get; set; }
    
    // Discrepancy details
    public List<DiscrepancyDetail> DiscrepancyDetails { get; set; } = new();
    public string? MatchMethod { get; set; }
    public string? MatchReason { get; set; }
}

/// <summary>
/// Details about a specific field discrepancy.
/// </summary>
public class DiscrepancyDetail
{
    public string FieldName { get; set; } = string.Empty;
    public string? ImportedValue { get; set; }
    public string? OpenFoodFactsValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Handler for GetComparisonResultsQuery.
/// </summary>
public class GetComparisonResultsHandler : IRequestHandler<GetComparisonResultsQuery, ComparisonResultsPage>
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public GetComparisonResultsHandler(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ComparisonResultsPage> Handle(
        GetComparisonResultsQuery request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var query = dbContext.ComparisonResults
            .Include(r => r.ImportedProduct)
            .Include(r => r.OpenFoodFactsProduct)
            .Where(r => r.ImportedProduct.TenantId == request.TenantId);

        // If ImportBatchId is provided and not Guid.Empty, filter by specific batch
        // If ImportBatchId is Guid.Empty, get results from ALL batches
        if (request.ImportBatchId.HasValue && request.ImportBatchId.Value != Guid.Empty)
        {
            query = query.Where(r => r.ImportedProduct.ImportBatchId == request.ImportBatchId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.MatchStatus == request.Status.Value);
        }

        // Apply search term filter across multiple fields
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(r =>
                r.ImportedProduct.Barcode.ToLower().Contains(searchLower) ||
                r.ImportedProduct.ProductName.ToLower().Contains(searchLower) ||
                (r.ImportedProduct.Brand != null && r.ImportedProduct.Brand.ToLower().Contains(searchLower)) ||
                (r.OpenFoodFactsProduct != null && r.OpenFoodFactsProduct.Barcode.ToLower().Contains(searchLower)) ||
                (r.OpenFoodFactsProduct != null && r.OpenFoodFactsProduct.ProductName != null && r.OpenFoodFactsProduct.ProductName.ToLower().Contains(searchLower)) ||
                (r.OpenFoodFactsProduct != null && r.OpenFoodFactsProduct.Brands != null && r.OpenFoodFactsProduct.Brands.ToLower().Contains(searchLower))
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var results = await query
            .OrderByDescending(r => r.ComparedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new
            {
                r.Id,
                Barcode = r.ImportedProduct.Barcode,
                ProductName = r.ImportedProduct.ProductName,
                Brand = r.ImportedProduct.Brand,
                r.MatchStatus,
                r.ConfidenceScore,
                OffProductName = r.OpenFoodFactsProduct != null ? r.OpenFoodFactsProduct.ProductName : null,
                OffBrand = r.OpenFoodFactsProduct != null ? r.OpenFoodFactsProduct.Brands : null,
                OffBarcode = r.OpenFoodFactsProduct != null ? r.OpenFoodFactsProduct.Barcode : null,
                ImageSmallUrl = r.OpenFoodFactsProduct != null ? r.OpenFoodFactsProduct.ImageSmallUrl : null,
                HasDiscrepancies = r.HasNameDiscrepancy || r.HasBrandDiscrepancy || r.HasCategoryDiscrepancy,
                r.ComparedAt,
                r.ComparisonDetails,
                r.HasNameDiscrepancy,
                r.HasBrandDiscrepancy,
                r.HasCategoryDiscrepancy,
                r.HasAllergenDiscrepancy,
                r.UsedSecondarySearch,
                r.SecondarySearchMethod
            })
            .ToListAsync(cancellationToken);

        var items = results.Select(r => 
        {
            var dto = new ComparisonResultDto
            {
                Id = r.Id,
                Barcode = r.Barcode,
                ProductName = r.ProductName,
                Brand = r.Brand,
                MatchStatus = r.MatchStatus,
                ConfidenceScore = r.ConfidenceScore,
                OffProductName = r.OffProductName,
                OffBrand = r.OffBrand,
                OffBarcode = r.OffBarcode,
                ImageUrl = r.ImageSmallUrl, // Use database URL only - cannot reliably construct
                HasDiscrepancies = r.HasDiscrepancies,
                ComparedAt = r.ComparedAt,
                UsedSecondarySearch = r.UsedSecondarySearch,
                SecondarySearchMethod = r.SecondarySearchMethod,
                DiscrepancyDetails = new List<DiscrepancyDetail>()
            };

            // Parse ComparisonDetails JSON to extract discrepancy information
            if (!string.IsNullOrEmpty(r.ComparisonDetails))
            {
                try
                {
                    using var doc = JsonDocument.Parse(r.ComparisonDetails);
                    var root = doc.RootElement;

                    // Extract match method and reason
                    if (root.TryGetProperty("matchMethod", out var matchMethod))
                        dto.MatchMethod = matchMethod.GetString();
                    
                    if (root.TryGetProperty("matchReason", out var matchReason))
                        dto.MatchReason = matchReason.GetString();

                    // Extract discrepancies
                    if (root.TryGetProperty("discrepancies", out var discrepancies))
                    {
                        foreach (var prop in discrepancies.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Object &&
                                prop.Name != "matchMethod" && prop.Name != "matchReason")
                            {
                                var detail = new DiscrepancyDetail
                                {
                                    FieldName = prop.Name
                                };

                                if (prop.Value.TryGetProperty("imported", out var imported))
                                    detail.ImportedValue = imported.GetString();
                                
                                if (prop.Value.TryGetProperty("openFoodFacts", out var off))
                                    detail.OpenFoodFactsValue = off.GetString();

                                detail.Reason = $"{prop.Name} mismatch";
                                dto.DiscrepancyDetails.Add(detail);
                            }
                        }
                    }

                    // Check for ProductName, Brand, Category, Allergens discrepancies
                    if (root.TryGetProperty("ProductName", out var productName) && productName.ValueKind == JsonValueKind.Object)
                    {
                        dto.DiscrepancyDetails.Add(new DiscrepancyDetail
                        {
                            FieldName = "Product Name",
                            ImportedValue = productName.TryGetProperty("imported", out var imp) ? imp.GetString() : null,
                            OpenFoodFactsValue = productName.TryGetProperty("openFoodFacts", out var off) ? off.GetString() : null,
                            Reason = "Product names do not match"
                        });
                    }

                    if (root.TryGetProperty("Brand", out var brand) && brand.ValueKind == JsonValueKind.Object)
                    {
                        dto.DiscrepancyDetails.Add(new DiscrepancyDetail
                        {
                            FieldName = "Brand",
                            ImportedValue = brand.TryGetProperty("imported", out var imp) ? imp.GetString() : null,
                            OpenFoodFactsValue = brand.TryGetProperty("openFoodFacts", out var off) ? off.GetString() : null,
                            Reason = "Brand names do not match"
                        });
                    }

                    if (root.TryGetProperty("Category", out var category) && category.ValueKind == JsonValueKind.Object)
                    {
                        dto.DiscrepancyDetails.Add(new DiscrepancyDetail
                        {
                            FieldName = "Category",
                            ImportedValue = category.TryGetProperty("imported", out var imp) ? imp.GetString() : null,
                            OpenFoodFactsValue = category.TryGetProperty("openFoodFacts", out var off) ? off.GetString() : null,
                            Reason = "Categories do not match"
                        });
                    }

                    if (root.TryGetProperty("Allergens", out var allergens) && allergens.ValueKind == JsonValueKind.Object)
                    {
                        dto.DiscrepancyDetails.Add(new DiscrepancyDetail
                        {
                            FieldName = "Allergens",
                            ImportedValue = allergens.TryGetProperty("imported", out var imp) ? imp.GetString() : null,
                            OpenFoodFactsValue = allergens.TryGetProperty("openFoodFacts", out var off) ? off.GetString() : null,
                            Reason = "Allergen information differs"
                        });
                    }

                    // Add reason for no match
                    if (root.TryGetProperty("reason", out var reason))
                    {
                        dto.MatchReason = reason.GetString();
                    }

                    // Add info about unmatched products
                    if (root.TryGetProperty("info", out var info))
                    {
                        if (dto.MatchReason == null)
                            dto.MatchReason = info.GetString();
                    }
                }
                catch
                {
                    // If JSON parsing fails, just continue without details
                }
            }

            return dto;
        }).ToList();

        return new ComparisonResultsPage
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
