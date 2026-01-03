using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Features.Database;

/// <summary>
/// Query to search Open Food Facts database by barcode.
/// </summary>
public record SearchOpenFoodFactsQuery(string Barcode) : IRequest<OpenFoodFactsProduct?>;

/// <summary>
/// Handler for searching Open Food Facts by barcode.
/// </summary>
public class SearchOpenFoodFactsHandler : IRequestHandler<SearchOpenFoodFactsQuery, OpenFoodFactsProduct?>
{
    private readonly AppDbContext _dbContext;
    private readonly IBarcodeNormalizer _barcodeNormalizer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearchOpenFoodFactsHandler> _logger;

    public SearchOpenFoodFactsHandler(
        AppDbContext dbContext,
        IBarcodeNormalizer barcodeNormalizer,
        IHttpClientFactory httpClientFactory,
        ILogger<SearchOpenFoodFactsHandler> logger)
    {
        _dbContext = dbContext;
        _barcodeNormalizer = barcodeNormalizer;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OpenFoodFactsProduct?> Handle(
        SearchOpenFoodFactsQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedBarcode = _barcodeNormalizer.Normalize(request.Barcode);

        // First, search local database
        var product = await _dbContext.OpenFoodFactsProducts
            .FirstOrDefaultAsync(p => p.NormalizedBarcode == normalizedBarcode, cancellationToken);

        if (product != null)
        {
            _logger.LogDebug("Found product {Barcode} in local database", request.Barcode);
            return product;
        }

        // If not found locally, try the API
        try
        {
            var client = _httpClientFactory.CreateClient("OpenFoodFacts");
            var response = await client.GetAsync($"/api/v2/product/{request.Barcode}.json", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                // Parse and return product from API
                // This is a simplified version - full implementation would parse the JSON
                _logger.LogDebug("Found product {Barcode} via API", request.Barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search Open Food Facts API for {Barcode}", request.Barcode);
        }

        return null;
    }
}

/// <summary>
/// Query to get sync status.
/// </summary>
public record GetSyncStatusQuery() : IRequest<SyncStatus?>;

/// <summary>
/// Handler for getting sync status.
/// </summary>
public class GetSyncStatusHandler : IRequestHandler<GetSyncStatusQuery, SyncStatus?>
{
    private readonly AppDbContext _dbContext;

    public GetSyncStatusHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SyncStatus?> Handle(GetSyncStatusQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.SyncStatuses
            .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", cancellationToken);
    }
}

/// <summary>
/// Command to start database sync.
/// </summary>
public record StartDatabaseSyncCommand(string? FilePath = null) : IRequest<bool>;

/// <summary>
/// Handler for starting database sync.
/// </summary>
public class StartDatabaseSyncHandler : IRequestHandler<StartDatabaseSyncCommand, bool>
{
    private readonly IOpenFoodFactsDataImporter _importer;
    private readonly ILogger<StartDatabaseSyncHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public StartDatabaseSyncHandler(
        IOpenFoodFactsDataImporter importer,
        ILogger<StartDatabaseSyncHandler> logger,
        IServiceScopeFactory scopeFactory)
    {
        _importer = importer;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<bool> Handle(StartDatabaseSyncCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Update sync status - use a fresh scope to avoid concurrency issues
            using (var initScope = _scopeFactory.CreateScope())
            {
                var initContext = initScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var syncStatus = await initContext.SyncStatuses
                    .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", cancellationToken);

                if (syncStatus == null)
                {
                    syncStatus = new SyncStatus { SourceName = "openfoodfacts" };
                    initContext.SyncStatuses.Add(syncStatus);
                }

                syncStatus.IsSyncing = true;
                syncStatus.LastSyncStarted = DateTime.UtcNow;
                syncStatus.StatusMessage = "Starting sync...";
                syncStatus.ProgressPercentage = 0;
                syncStatus.TotalProductsSynced = 0;
                syncStatus.LastError = null;
                syncStatus.LastErrorAt = null;
                await initContext.SaveChangesAsync(cancellationToken);
            }

            // Create progress reporter that updates the database using a NEW DbContext
            var progress = new Progress<ImportProgress>(async p =>
            {
                try
                {
                    // Create a new DbContext instance for thread safety
                    using var scope = _scopeFactory.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    var status = await scopedContext.SyncStatuses
                        .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", cancellationToken);
                    
                    if (status != null)
                    {
                        status.StatusMessage = p.Status;
                        status.TotalProductsSynced = p.ProcessedCount;
                        status.DownloadedBytes = p.ProcessedCount > 0 ? p.ProcessedCount : null;
                        status.TotalBytes = p.TotalCount > 0 ? p.TotalCount : null;
                        
                        // Calculate progress percentage
                        if (p.TotalCount > 0)
                        {
                            status.ProgressPercentage = (int)((double)p.ProcessedCount / p.TotalCount * 100);
                        }
                        
                        status.LastUpdated = DateTime.UtcNow;
                        await scopedContext.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update sync progress");
                }
            });

            // Start import
            ImportProgress result;
            if (!string.IsNullOrEmpty(request.FilePath))
            {
                result = await _importer.ImportFromFileAsync(request.FilePath, progress, cancellationToken);
            }
            else
            {
                result = await _importer.DownloadAndImportAsync(progress, cancellationToken);
            }

            // Update final status - use a new scope to avoid context issues
            using (var scope = _scopeFactory.CreateScope())
            {
                var finalContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var finalStatus = await finalContext.SyncStatuses
                    .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", cancellationToken);
                
                if (finalStatus != null)
                {
                    finalStatus.IsSyncing = false;
                    finalStatus.LastSyncCompleted = DateTime.UtcNow;
                    finalStatus.TotalProductsSynced = result.ProcessedCount;
                    finalStatus.StatusMessage = result.Status;
                    finalStatus.LastError = result.ErrorMessage;
                    finalStatus.ProgressPercentage = 100;
                    await finalContext.SaveChangesAsync(cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database sync failed");
            
            // Update status with error - use a new scope to avoid disposed context issues
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var syncStatus = await dbContext.SyncStatuses
                    .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", CancellationToken.None);
                
                if (syncStatus != null)
                {
                    syncStatus.IsSyncing = false;
                    syncStatus.LastError = ex.Message;
                    syncStatus.LastErrorAt = DateTime.UtcNow;
                    syncStatus.StatusMessage = "Sync failed";
                    await dbContext.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception scopeEx)
            {
                _logger.LogError(scopeEx, "Failed to update sync status after error");
            }
            
            return false;
        }
    }
}

/// <summary>
/// Query to get database statistics.
/// </summary>
public record GetDatabaseStatsQuery() : IRequest<DatabaseStats>;

public class DatabaseStats
{
    public int TotalProducts { get; set; }
    public int ProductsWithImages { get; set; }
    public int ProductsWithNutrition { get; set; }
}

/// <summary>
/// Handler for getting database statistics.
/// </summary>
public class GetDatabaseStatsHandler : IRequestHandler<GetDatabaseStatsQuery, DatabaseStats>
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public GetDatabaseStatsHandler(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DatabaseStats> Handle(GetDatabaseStatsQuery request, CancellationToken cancellationToken)
    {
        // Create a fresh DbContext for this query to avoid concurrency issues
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var stats = new DatabaseStats
        {
            TotalProducts = await dbContext.OpenFoodFactsProducts.CountAsync(cancellationToken),
            ProductsWithImages = await dbContext.OpenFoodFactsProducts.CountAsync(p => p.ImageUrl != null, cancellationToken),
            ProductsWithNutrition = await dbContext.OpenFoodFactsProducts.CountAsync(p => p.EnergyKcal100g != null, cancellationToken)
        };

        return stats;
    }
}
