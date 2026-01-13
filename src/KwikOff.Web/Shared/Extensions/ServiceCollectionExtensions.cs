using KwikOff.Web.Infrastructure.BackgroundServices;
using KwikOff.Web.Infrastructure.Services;
using KwikOff.Web.Infrastructure.Services.Mappers;

namespace KwikOff.Web.Shared.Extensions;

/// <summary>
/// Extension methods for registering KwikOff services in the DI container.
/// Following SOLID principles - each service has a single responsibility.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all KwikOff application services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddKwikOffServices(this IServiceCollection services)
    {
        // Memory cache for AI service
        services.AddMemoryCache();

        // File Processing Services
        services.AddScoped<ICsvProductReader, CsvProductReader>();
        services.AddScoped<IExcelProductReader, ExcelProductReader>();
        services.AddScoped<IBarcodeNormalizer, BarcodeNormalizer>();
        services.AddScoped<ISmartValueDetector, SmartValueDetector>();

        // Name and Brand Processing Services
        services.AddScoped<INameNormalizer, NameNormalizer>();
        services.AddScoped<IBrandExtractor, BrandExtractor>();

        // Field Detection Services (Strategy Pattern)
        services.AddScoped<IColumnMappingValidator, ColumnMappingValidator>();
        services.AddScoped<IColumnDetectionService, ColumnDetectionService>();

        // Comparison Services
        services.AddScoped<IComparisonService, ComparisonService>();

        // AI Services for name normalization
        services.AddScoped<IAIService, OpenAIService>();
        services.AddHttpClient<OpenAIService>();

        // Export Services
        services.AddScoped<ICsvExporter, CsvExporter>();
        services.AddScoped<IJsonExporter, JsonExporter>();
        
        // Image URL Services
        services.AddHttpClient<IImageUrlService, ImageUrlService>(client =>
        {
            client.BaseAddress = new Uri("https://world.openfoodfacts.org");
            client.DefaultRequestHeaders.Add("User-Agent", "KwikOff/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Open Food Facts Services
        services.AddScoped<IOpenFoodFactsDataImporter, OpenFoodFactsDataImporter>();

        // KwikKart Integration Services
        services.AddScoped<KwikKartMapper>();
        services.AddScoped<IKwikKartIntegrationService, KwikKartIntegrationService>();

        // Background Services - DISABLED to prevent auto-sync on startup
        // services.AddHostedService<SyncBackgroundService>();

        return services;
    }
}
