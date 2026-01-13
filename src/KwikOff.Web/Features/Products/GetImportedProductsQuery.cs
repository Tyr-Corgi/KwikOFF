using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Features.Products;

/// <summary>
/// Query to get imported products with pagination.
/// </summary>
public record GetImportedProductsQuery(
    string TenantId,
    int Page = 1,
    int PageSize = 10,
    Guid? BatchId = null,
    string? SearchTerm = null
) : IRequest<PagedResult<ImportedProduct>>;

/// <summary>
/// Paged result for queries.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Handler for GetImportedProductsQuery.
/// </summary>
public class GetImportedProductsHandler : IRequestHandler<GetImportedProductsQuery, PagedResult<ImportedProduct>>
{
    private readonly AppDbContext _dbContext;

    public GetImportedProductsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ImportedProduct>> Handle(
        GetImportedProductsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ImportedProducts
            .Where(p => p.TenantId == request.TenantId);

        // Filter by batch if specified
        if (request.BatchId.HasValue)
        {
            query = query.Where(p => p.ImportBatchId == request.BatchId.Value);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(p =>
                p.Barcode.ToLower().Contains(term) ||
                p.ProductName.ToLower().Contains(term) ||
                (p.Brand != null && p.Brand.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.ImportedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ImportedProduct>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

/// <summary>
/// Query to get import batch summary.
/// </summary>
public record GetImportBatchesQuery(
    string? TenantId = null,  // Optional - null means get all tenants
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedResult<ImportBatchSummary>>;

/// <summary>
/// Summary of an import batch.
/// </summary>
public class ImportBatchSummary
{
    public Guid BatchId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public int ProductCount { get; set; }
    public DateTime ImportedAt { get; set; }
}

/// <summary>
/// Handler for GetImportBatchesQuery.
/// </summary>
public class GetImportBatchesHandler : IRequestHandler<GetImportBatchesQuery, PagedResult<ImportBatchSummary>>
{
    private readonly AppDbContext _dbContext;

    public GetImportBatchesHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ImportBatchSummary>> Handle(
        GetImportBatchesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ImportedProducts.AsQueryable();
        
        // Filter by tenant ID if specified
        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            query = query.Where(p => p.TenantId == request.TenantId);
        }

        // Materialize the grouped query once to avoid double enumeration issues
        var allBatches = await query
            .GroupBy(p => new { p.ImportBatchId, p.TenantId, p.FileName })
            .Select(g => new ImportBatchSummary
            {
                BatchId = g.Key.ImportBatchId,
                TenantId = g.Key.TenantId,
                FileName = g.Key.FileName,
                ProductCount = g.Count(),
                ImportedAt = g.Min(p => p.ImportedAt)
            })
            .OrderByDescending(b => b.ImportedAt)
            .ToListAsync(cancellationToken);

        var totalCount = allBatches.Count;

        var items = allBatches
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<ImportBatchSummary>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
