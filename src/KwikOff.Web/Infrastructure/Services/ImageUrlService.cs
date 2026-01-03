using System.Text.Json;
using KwikOff.Web.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for fetching and caching product image URLs from Open Food Facts API.
/// </summary>
public interface IImageUrlService
{
    Task<ProductImageUrls?> FetchAndCacheImageUrlsAsync(string barcode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Product image URLs from Open Food Facts.
/// </summary>
public class ProductImageUrls
{
    public string? ImageUrl { get; set; }
    public string? ImageSmallUrl { get; set; }
    public string? ImageFrontUrl { get; set; }
    public string? ImageIngredientsUrl { get; set; }
    public string? ImageNutritionUrl { get; set; }
}

public class ImageUrlService : IImageUrlService
{
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<ImageUrlService> _logger;

    public ImageUrlService(
        HttpClient httpClient,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<ImageUrlService> logger)
    {
        _httpClient = httpClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<ProductImageUrls?> FetchAndCacheImageUrlsAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a new DbContext for this operation
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Check if we already have cached image URLs
            var product = await dbContext.OpenFoodFactsProducts
                .FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken);

            if (product == null)
            {
                _logger.LogWarning("Product with barcode {Barcode} not found in database", barcode);
                return null;
            }

            // If we already have cached images, return them
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                return new ProductImageUrls
                {
                    ImageUrl = product.ImageUrl,
                    ImageSmallUrl = product.ImageSmallUrl,
                    ImageFrontUrl = product.ImageFrontUrl,
                    ImageIngredientsUrl = product.ImageIngredientsUrl,
                    ImageNutritionUrl = product.ImageNutritionUrl
                };
            }

            // Fetch from Open Food Facts API
            _logger.LogInformation("Fetching image URLs from OFF API for barcode: {Barcode}", barcode);
            var response = await _httpClient.GetAsync(
                $"https://world.openfoodfacts.org/api/v2/product/{barcode}.json",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OFF API returned {StatusCode} for barcode {Barcode}", 
                    response.StatusCode, barcode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(jsonContent);

            // Check if product exists in OFF
            if (!jsonDoc.RootElement.TryGetProperty("product", out var productElement))
            {
                _logger.LogInformation("Product {Barcode} not found in OFF", barcode);
                return null;
            }

            // Extract image URLs
            var imageUrls = new ProductImageUrls
            {
                ImageUrl = GetStringProperty(productElement, "image_url"),
                ImageSmallUrl = GetStringProperty(productElement, "image_small_url"),
                ImageFrontUrl = GetStringProperty(productElement, "image_front_url"),
                ImageIngredientsUrl = GetStringProperty(productElement, "image_ingredients_url"),
                ImageNutritionUrl = GetStringProperty(productElement, "image_nutrition_url")
            };

            // Cache the URLs in our database
            if (!string.IsNullOrEmpty(imageUrls.ImageUrl))
            {
                product.ImageUrl = imageUrls.ImageUrl;
                product.ImageSmallUrl = imageUrls.ImageSmallUrl;
                product.ImageFrontUrl = imageUrls.ImageFrontUrl;
                product.ImageIngredientsUrl = imageUrls.ImageIngredientsUrl;
                product.ImageNutritionUrl = imageUrls.ImageNutritionUrl;

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Cached image URLs for barcode {Barcode}", barcode);
            }

            return imageUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching image URLs for barcode {Barcode}", barcode);
            return null;
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && 
            property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return null;
    }
}


