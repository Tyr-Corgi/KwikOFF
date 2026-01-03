using KwikOff.Web.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Features.Products;

/// <summary>
/// Query to count products matching a filter.
/// </summary>
public record CountFilteredProductsQuery(
    Guid BatchId,
    string TenantId,
    string? SearchFilter
) : IRequest<int>;

public class CountFilteredProductsHandler : IRequestHandler<CountFilteredProductsQuery, int>
{
    private readonly AppDbContext _dbContext;

    public CountFilteredProductsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> Handle(CountFilteredProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.ImportedProducts
            .Where(p => p.ImportBatchId == request.BatchId && p.TenantId == request.TenantId);

        if (!string.IsNullOrWhiteSpace(request.SearchFilter))
        {
            var filter = request.SearchFilter.ToLower();
            query = query.Where(p =>
                p.Barcode.ToLower().Contains(filter) ||
                (p.ProductName != null && p.ProductName.ToLower().Contains(filter)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(filter)));
        }

        return await query.CountAsync(cancellationToken);
    }
}


