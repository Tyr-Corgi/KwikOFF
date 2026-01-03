using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Features.Database;

/// <summary>
/// Query to search products by any text (name, brand, category, barcode)
/// </summary>
public record SearchProductsQuery(string SearchText, int Page = 1, int PageSize = 20) : IRequest<SearchProductsResult>;

/// <summary>
/// Result of product search
/// </summary>
public record SearchProductsResult(
    List<OpenFoodFactsProduct> Products,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for searching products by text
/// </summary>
public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, SearchProductsResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SearchProductsHandler> _logger;

    public SearchProductsHandler(
        AppDbContext dbContext,
        ILogger<SearchProductsHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SearchProductsResult> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SearchText))
        {
            return new SearchProductsResult(new List<OpenFoodFactsProduct>(), 0, 1, request.PageSize, 0);
        }

        var searchTerm = request.SearchText.Trim().ToLower();
        
        _logger.LogInformation("Searching products for: {SearchTerm}", searchTerm);

        // Build query with full-text search across multiple fields
        var query = _dbContext.OpenFoodFactsProducts
            .Where(p =>
                // Barcode search (exact or partial)
                EF.Functions.Like(p.Barcode.ToLower(), $"%{searchTerm}%") ||
                EF.Functions.Like(p.NormalizedBarcode.ToLower(), $"%{searchTerm}%") ||
                
                // Product name search
                (p.ProductName != null && EF.Functions.Like(p.ProductName.ToLower(), $"%{searchTerm}%")) ||
                
                // Brand search
                (p.Brands != null && EF.Functions.Like(p.Brands.ToLower(), $"%{searchTerm}%")) ||
                
                // Category search
                (p.Categories != null && EF.Functions.Like(p.Categories.ToLower(), $"%{searchTerm}%")) ||
                (p.CategoriesTags != null && EF.Functions.Like(p.CategoriesTags.ToLower(), $"%{searchTerm}%")) ||
                
                // Generic name search
                (p.GenericName != null && EF.Functions.Like(p.GenericName.ToLower(), $"%{searchTerm}%"))
            )
            .OrderByDescending(p => 
                // Prioritize exact barcode matches
                p.Barcode.ToLower() == searchTerm ? 1000 :
                // Then product name matches
                (p.ProductName != null && p.ProductName.ToLower().StartsWith(searchTerm)) ? 500 :
                // Then brand matches
                (p.Brands != null && p.Brands.ToLower().Contains(searchTerm)) ? 250 :
                // Finally category matches
                100
            )
            .ThenBy(p => p.ProductName);

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        // Get paginated results
        var products = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} products matching '{SearchTerm}'", totalCount, searchTerm);

        return new SearchProductsResult(
            products,
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}


