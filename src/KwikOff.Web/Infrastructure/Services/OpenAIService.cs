using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// OpenAI-based AI service for product name normalization.
/// </summary>
public class OpenAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenAIService> _logger;
    
    private const string CacheKeyPrefix = "AI_Normalized_";

    public OpenAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> NormalizeProductNameAsync(string productName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return productName;

        // Check cache first
        var cacheKey = $"{CacheKeyPrefix}{productName}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedResult))
        {
            return cachedResult!;
        }

        try
        {
            var provider = _configuration["AI:Provider"] ?? "OpenAI";
            var apiKey = _configuration["AI:ApiKey"];
            var model = _configuration["AI:Model"] ?? "gpt-3.5-turbo";
            var maxTokens = int.Parse(_configuration["AI:MaxTokens"] ?? "50");

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("AI API key not configured, returning original product name");
                return productName;
            }

            // Prepare the request
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a product name normalizer. Normalize product names by: 1) Removing size/quantity indicators (oz, ml, lb, g, etc), 2) Standardizing punctuation and spacing, 3) Keeping brand names intact, 4) Using title case. Return ONLY the normalized name, no explanations."
                    },
                    new
                    {
                        role = "user",
                        content = $"Normalize: {productName}"
                    }
                },
                max_tokens = maxTokens,
                temperature = 0.3 // Lower temperature for more consistent results
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
                return productName;
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);
            var normalizedName = result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? productName;

            // Cache the result for 1 hour
            var cacheExpiration = TimeSpan.Parse(_configuration["AI:CacheExpiration"] ?? "01:00:00");
            _cache.Set(cacheKey, normalizedName, cacheExpiration);

            _logger.LogDebug("Normalized '{Original}' to '{Normalized}'", productName, normalizedName);

            return normalizedName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to normalize product name '{ProductName}'", productName);
            return productName; // Fallback to original name
        }
    }

    #region OpenAI Response Models

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    #endregion
}


