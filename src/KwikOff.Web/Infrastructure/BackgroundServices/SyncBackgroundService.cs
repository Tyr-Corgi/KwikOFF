using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for syncing Open Food Facts database.
/// </summary>
public class SyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if sync is needed
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var syncStatus = await dbContext.SyncStatuses
                    .FirstOrDefaultAsync(s => s.SourceName == "openfoodfacts", stoppingToken);

                // Only auto-sync if no products exist or last sync was more than 7 days ago
                var shouldSync = syncStatus == null ||
                    (syncStatus.LastSyncCompleted == null && !syncStatus.IsSyncing) ||
                    (syncStatus.LastSyncCompleted.HasValue &&
                     (DateTime.UtcNow - syncStatus.LastSyncCompleted.Value).TotalDays > 7);

                if (shouldSync)
                {
                    _logger.LogInformation("Auto-sync triggered for Open Food Facts database");
                    // Note: Actual sync would be triggered manually or on-demand
                    // to avoid unexpected large downloads
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync background service");
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
