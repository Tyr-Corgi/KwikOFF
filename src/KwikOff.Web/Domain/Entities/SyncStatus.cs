namespace KwikOff.Web.Domain.Entities;

/// <summary>
/// Tracks the synchronization status of the Open Food Facts database.
/// Monitors background sync operations and provides status to users.
/// </summary>
public class SyncStatus
{
    public long Id { get; set; }

    /// <summary>
    /// Unique identifier for the sync source (e.g., "openfoodfacts").
    /// </summary>
    public string SourceName { get; set; } = "openfoodfacts";

    /// <summary>
    /// Whether a sync operation is currently in progress.
    /// </summary>
    public bool IsSyncing { get; set; }

    /// <summary>
    /// When the current/last sync operation started.
    /// </summary>
    public DateTime? LastSyncStarted { get; set; }

    /// <summary>
    /// When the last sync operation completed successfully.
    /// </summary>
    public DateTime? LastSyncCompleted { get; set; }

    /// <summary>
    /// Total number of products synced from source.
    /// </summary>
    public long TotalProductsSynced { get; set; }

    /// <summary>
    /// Number of products in current sync batch.
    /// </summary>
    public long CurrentBatchCount { get; set; }

    /// <summary>
    /// Progress percentage (0-100) of current sync.
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// Current status message for display.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Last error message if sync failed.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the last error occurred.
    /// </summary>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>
    /// Size of the downloaded database file in bytes.
    /// </summary>
    public long? DownloadedBytes { get; set; }

    /// <summary>
    /// Total size of the database file in bytes.
    /// </summary>
    public long? TotalBytes { get; set; }

    /// <summary>
    /// Last time this record was updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
