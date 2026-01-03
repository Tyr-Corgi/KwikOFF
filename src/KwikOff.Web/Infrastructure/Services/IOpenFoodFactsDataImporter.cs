namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for importing Open Food Facts database.
/// Handles the large 10GB+ JSONL database with ~3.7M products.
/// </summary>
public interface IOpenFoodFactsDataImporter
{
    /// <summary>
    /// Imports products from the Open Food Facts JSONL database.
    /// </summary>
    Task<ImportProgress> ImportFromFileAsync(
        string filePath, 
        IProgress<ImportProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and imports the latest Open Food Facts database.
    /// </summary>
    Task<ImportProgress> DownloadAndImportAsync(
        IProgress<ImportProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current import status.
    /// </summary>
    Task<ImportProgress> GetCurrentProgressAsync();
}

/// <summary>
/// Progress information for Open Food Facts database import.
/// </summary>
public class ImportProgress
{
    public bool IsRunning { get; set; }
    public long ProcessedCount { get; set; }
    public long TotalCount { get; set; }
    public int ProgressPercentage => TotalCount > 0 ? (int)(ProcessedCount * 100 / TotalCount) : 0;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Elapsed => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
    
    // Download progress
    public long? DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }
}

