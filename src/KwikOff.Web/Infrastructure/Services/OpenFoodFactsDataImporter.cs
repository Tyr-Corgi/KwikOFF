using System.IO.Compression;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services.BatchProcessing;
using KwikOff.Web.Infrastructure.Services.Parsers;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Imports Open Food Facts data from JSONL files.
/// Optimized for the large 10GB+ database with ~3.7M products.
/// </summary>
public class OpenFoodFactsDataImporter : IOpenFoodFactsDataImporter
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenFoodFactsDataImporter> _logger;
    private readonly OpenFoodFactsParser _jsonParser;
    private readonly OpenFoodFactsCsvParser _csvParser;
    private readonly OpenFoodFactsBatchSaver _batchSaver;

    private const int BatchSize = 5000;
    private static ImportProgress _currentProgress = new();
    private static readonly object _lock = new();

    public OpenFoodFactsDataImporter(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IBarcodeNormalizer barcodeNormalizer,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<OpenFoodFactsDataImporter>();
        
        // Initialize focused components
        _jsonParser = new OpenFoodFactsParser(
            barcodeNormalizer, 
            loggerFactory.CreateLogger<OpenFoodFactsParser>());
        _csvParser = new OpenFoodFactsCsvParser(
            barcodeNormalizer,
            loggerFactory.CreateLogger<OpenFoodFactsCsvParser>());
        _batchSaver = new OpenFoodFactsBatchSaver(
            dbContextFactory, 
            loggerFactory.CreateLogger<OpenFoodFactsBatchSaver>());
    }

    public Task<ImportProgress> GetCurrentProgressAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(new ImportProgress
            {
                IsRunning = _currentProgress.IsRunning,
                ProcessedCount = _currentProgress.ProcessedCount,
                TotalCount = _currentProgress.TotalCount,
                Status = _currentProgress.Status,
                ErrorMessage = _currentProgress.ErrorMessage,
                StartedAt = _currentProgress.StartedAt,
                CompletedAt = _currentProgress.CompletedAt,
                DownloadedBytes = _currentProgress.DownloadedBytes,
                TotalBytes = _currentProgress.TotalBytes
            });
        }
    }

    public async Task<ImportProgress> DownloadAndImportAsync(
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadUrl = _configuration["OpenFoodFacts:DatabaseDownloadUrl"]
            ?? "https://static.openfoodfacts.org/data/en.openfoodfacts.org.products.csv";

        // Determine file extension from URL
        var isCsv = downloadUrl.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var isGzipped = downloadUrl.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var fileExtension = isCsv ? ".csv" : ".jsonl";
        if (isGzipped) fileExtension += ".gz";
        
        var tempPath = Path.Combine(Path.GetTempPath(), $"openfoodfacts-products{fileExtension}");

        try
        {
            UpdateProgress(true, 0, 0, "Downloading database...");
            progress?.Report(_currentProgress);

            await DownloadFileAsync(downloadUrl, tempPath, progress, cancellationToken);

            _logger.LogInformation("Downloaded to {Path}", tempPath);

            // Import the downloaded file
            return await ImportFromFileAsync(tempPath, progress, cancellationToken);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    public async Task<ImportProgress> ImportFromFileAsync(
        string filePath, 
        IProgress<ImportProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        UpdateProgress(true, 0, 0, "Starting import...");
        progress?.Report(_currentProgress);

        // Detect file format
        var isCsv = filePath.Contains(".csv", StringComparison.OrdinalIgnoreCase);
        
        if (isCsv)
        {
            return await ImportFromCsvFileAsync(filePath, progress, cancellationToken);
        }
        else
        {
            return await ImportFromJsonlFileAsync(filePath, progress, cancellationToken);
        }
    }

    private async Task<ImportProgress> ImportFromCsvFileAsync(
        string filePath,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            Stream fileStream = File.OpenRead(filePath);

            // Handle gzipped files
            if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                fileStream = new GZipStream(fileStream, CompressionMode.Decompress);
            }

            _logger.LogInformation("Parsing CSV file: {FilePath}", filePath);
            
            var products = new List<OpenFoodFactsProduct>();
            long importedCount = 0;
            var estimatedLines = EstimateLineCount(filePath);

            UpdateProgress(true, 0, estimatedLines, "Parsing CSV products...");

            // Use CSV parser to stream products
            foreach (var product in _csvParser.ParseCsvStream(fileStream))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                products.Add(product);

                // Batch insert
                if (products.Count >= BatchSize)
                {
                    await _batchSaver.SaveBatchAsync(products, cancellationToken);
                    importedCount += products.Count;
                    products.Clear();

                    UpdateProgress(
                        true,
                        importedCount,
                        Math.Max(estimatedLines, importedCount),
                        $"Imported {importedCount:N0} products...");
                    progress?.Report(_currentProgress);

                    _logger.LogInformation("Imported {Count} products", importedCount);
                }
            }

            // Save remaining products
            if (products.Count > 0)
            {
                await _batchSaver.SaveBatchAsync(products, cancellationToken);
                importedCount += products.Count;
            }

            fileStream.Dispose();

            UpdateProgress(false, importedCount, importedCount, "Import completed");
            _currentProgress.CompletedAt = DateTime.UtcNow;
            progress?.Report(_currentProgress);

            _logger.LogInformation("CSV Import completed. Total products: {Count}", importedCount);

            return _currentProgress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV Import failed");
            UpdateProgress(false, _currentProgress.ProcessedCount, _currentProgress.TotalCount,
                "Import failed", ex.Message);
            throw;
        }
    }

    private async Task<ImportProgress> ImportFromJsonlFileAsync(
        string filePath,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            Stream fileStream = File.OpenRead(filePath);

            // Handle gzipped files
            if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                fileStream = new GZipStream(fileStream, CompressionMode.Decompress);
            }

            using var reader = new StreamReader(fileStream);
            var products = new List<OpenFoodFactsProduct>();
            long lineCount = 0;
            long importedCount = 0;

            // Estimate total lines (rough estimate based on file size)
            var estimatedLines = EstimateLineCount(filePath);
            UpdateProgress(true, 0, estimatedLines, "Parsing JSONL products...");

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                lineCount++;

                var product = _jsonParser.ParseProduct(line);
                if (product != null)
                {
                    products.Add(product);
                }

                // Batch insert
                if (products.Count >= BatchSize)
                {
                    await _batchSaver.SaveBatchAsync(products, cancellationToken);
                    importedCount += products.Count;
                    products.Clear();

                    UpdateProgress(
                        true, 
                        importedCount, 
                        Math.Max(estimatedLines, importedCount),
                        $"Imported {importedCount:N0} products...");
                    progress?.Report(_currentProgress);

                    _logger.LogInformation("Imported {Count} products", importedCount);
                }
            }

            // Save remaining products
            if (products.Count > 0)
            {
                await _batchSaver.SaveBatchAsync(products, cancellationToken);
                importedCount += products.Count;
            }

            UpdateProgress(false, importedCount, importedCount, "Import completed");
            _currentProgress.CompletedAt = DateTime.UtcNow;
            progress?.Report(_currentProgress);

            _logger.LogInformation("JSONL Import completed. Total products: {Count}", importedCount);

            return _currentProgress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSONL Import failed");
            UpdateProgress(false, _currentProgress.ProcessedCount, _currentProgress.TotalCount,
                "Import failed", ex.Message);
            throw;
        }
    }

    private async Task DownloadFileAsync(
        string url, 
        string destinationPath, 
        IProgress<ImportProgress>? progress, 
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("OpenFoodFacts");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = File.Create(destinationPath);
        
        var buffer = new byte[81920]; // 80KB buffer
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percentage = (int)(totalRead * 100 / totalBytes);
                UpdateProgress(true, totalRead, totalBytes, $"Downloading... {percentage}%");
                progress?.Report(_currentProgress);
            }
        }
    }

    private static long EstimateLineCount(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? fileInfo.Length / 100  // Compressed: ~100 bytes per line
            : fileInfo.Length / 2000; // Uncompressed: ~2KB per product
    }

    private void UpdateProgress(bool isRunning, long processed, long total, string status, string? error = null)
    {
        lock (_lock)
        {
            _currentProgress.IsRunning = isRunning;
            _currentProgress.ProcessedCount = processed;
            _currentProgress.TotalCount = total;
            _currentProgress.Status = status;
            _currentProgress.ErrorMessage = error;
            
            // For download progress, set the download bytes
            if (status.Contains("Downloading"))
            {
                _currentProgress.DownloadedBytes = processed;
                _currentProgress.TotalBytes = total;
            }
            
            if (isRunning && _currentProgress.StartedAt == default)
            {
                _currentProgress.StartedAt = DateTime.UtcNow;
            }
        }
    }
}
