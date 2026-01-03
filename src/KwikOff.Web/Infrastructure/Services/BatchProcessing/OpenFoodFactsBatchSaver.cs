using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Services.BatchProcessing;

/// <summary>
/// Handles batch saving of Open Food Facts products to the database.
/// Implements resilient error handling for crowdsourced data.
/// </summary>
public class OpenFoodFactsBatchSaver
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<OpenFoodFactsBatchSaver> _logger;

    public OpenFoodFactsBatchSaver(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<OpenFoodFactsBatchSaver> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Saves a batch of products to the database.
    /// Uses bulk insert for speed, falls back to individual inserts on errors.
    /// </summary>
    public async Task SaveBatchAsync(List<OpenFoodFactsProduct> products, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Try bulk insert first (fast path)
        await dbContext.OpenFoodFactsProducts.AddRangeAsync(products, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // On error, fall back to individual inserts to isolate problem records
            await SaveIndividuallyAsync(dbContext, products, cancellationToken);
        }
    }

    private async Task SaveIndividuallyAsync(
        AppDbContext dbContext,
        List<OpenFoodFactsProduct> products,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        
        int successCount = 0;
        int skipCount = 0;
        
        foreach (var product in products)
        {
            try
            {
                dbContext.OpenFoodFactsProducts.Add(product);
                await dbContext.SaveChangesAsync(cancellationToken);
                successCount++;
            }
            catch (DbUpdateException innerEx)
            {
                // Skip problematic records (duplicates, data issues, etc.)
                skipCount++;
                dbContext.ChangeTracker.Clear();
                
                // Log only unexpected errors (not duplicates or encoding issues)
                if (innerEx.InnerException is Npgsql.PostgresException pgEx)
                {
                    if (pgEx.SqlState != "23505" && pgEx.SqlState != "22021") // Not duplicate or encoding
                    {
                        _logger.LogWarning(
                            "Skipped product {Barcode} due to error: {Error}", 
                            product.Barcode, 
                            pgEx.MessageText);
                    }
                }
            }
        }
        
        if (skipCount > 0)
        {
            _logger.LogInformation(
                "Batch completed: {Success} saved, {Skipped} skipped", 
                successCount, 
                skipCount);
        }
    }
}


