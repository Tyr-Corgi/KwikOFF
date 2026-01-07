using KwikOff.Web.Features.Comparison;
using KwikOff.Web.Infrastructure.Services.Mappers;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for integrating with KwikKart retail management system
/// </summary>
public interface IKwikKartIntegrationService
{
    Task<KwikKartExportResult> ExportToKwikKart(List<ComparisonResultDto> results, string tenantId);
    Task<bool> TestConnection();
}

public class KwikKartIntegrationService : IKwikKartIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly KwikKartMapper _mapper;
    private readonly ILogger<KwikKartIntegrationService> _logger;
    private readonly KwikKartSettings _settings;

    public KwikKartIntegrationService(
        IHttpClientFactory httpClientFactory,
        KwikKartMapper mapper,
        IOptions<KwikKartSettings> settings,
        ILogger<KwikKartIntegrationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KwikKart");
        _mapper = mapper;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Export enriched product data to KwikKart
    /// </summary>
    public async Task<KwikKartExportResult> ExportToKwikKart(List<ComparisonResultDto> results, string tenantId)
    {
        var result = new KwikKartExportResult
        {
            TenantId = tenantId,
            TotalProducts = results.Count,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Starting export to KwikKart for tenant {TenantId} with {Count} products",
                tenantId, results.Count);

            // Filter to only matched products
            var matchedProducts = results
                .Where(r => r.MatchStatus == Domain.Enums.MatchStatus.Matched)
                .ToList();

            result.MatchedProducts = matchedProducts.Count;

            if (matchedProducts.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No matched products to export";
                return result;
            }

            // Map to KwikKart format
            var request = _mapper.MapToReconciliationRequest(matchedProducts, tenantId);

            // Send to KwikKart API
            var response = await _httpClient.PostAsJsonAsync(
                "/api/products/reconciliation/run",
                request);

            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadFromJsonAsync<KwikKartReconciliationResponse>();
                
                result.Success = responseData?.Success ?? false;
                result.ProductsExported = responseData?.ProductsProcessed ?? 0;
                result.ProductsCreated = responseData?.ProductsCreated ?? 0;
                result.ProductsUpdated = responseData?.ProductsUpdated ?? 0;
                result.ProductsSkipped = responseData?.ProductsSkipped ?? 0;
                result.ErrorMessage = responseData?.Error;

                _logger.LogInformation(
                    "KwikKart export completed: {Created} created, {Updated} updated, {Skipped} skipped",
                    result.ProductsCreated, result.ProductsUpdated, result.ProductsSkipped);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Success = false;
                result.ErrorMessage = $"KwikKart API error: {response.StatusCode} - {errorContent}";
                
                _logger.LogError(
                    "KwikKart export failed: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to KwikKart");
            result.Success = false;
            result.ErrorMessage = $"Exception: {ex.Message}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Test connection to KwikKart API
    /// </summary>
    public async Task<bool> TestConnection()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to KwikKart");
            return false;
        }
    }
}

// Settings
public class KwikKartSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 300; // 5 minutes for large exports
    public bool EnableIntegration { get; set; } = true;
}

// Result DTOs
public class KwikKartExportResult
{
    public bool Success { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int MatchedProducts { get; set; }
    public int ProductsExported { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int ProductsSkipped { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
}

public class KwikKartReconciliationResponse
{
    public bool Success { get; set; }
    public int ProductsProcessed { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int ProductsSkipped { get; set; }
    public string? Error { get; set; }
}

