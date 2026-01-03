namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// AI service for product data normalization and enhancement.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Normalizes a product name using AI to standardize format, remove size indicators, and fix punctuation.
    /// </summary>
    /// <param name="productName">The product name to normalize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Normalized product name</returns>
    Task<string> NormalizeProductNameAsync(string productName, CancellationToken cancellationToken = default);
}


