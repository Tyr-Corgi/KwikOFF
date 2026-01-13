using System.Text.Json;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Domain.Enums;
using KwikOff.Web.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Compares imported products against Open Food Facts database.
/// </summary>
public class ComparisonService : IComparisonService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IBarcodeNormalizer _barcodeNormalizer;
    private readonly IAIService _aiService;
    private readonly INameNormalizer _nameNormalizer;
    private readonly IBrandExtractor _brandExtractor;
    private readonly ILogger<ComparisonService> _logger;

    public ComparisonService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IBarcodeNormalizer barcodeNormalizer,
        IAIService aiService,
        INameNormalizer nameNormalizer,
        IBrandExtractor brandExtractor,
        ILogger<ComparisonService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _barcodeNormalizer = barcodeNormalizer;
        _aiService = aiService;
        _nameNormalizer = nameNormalizer;
        _brandExtractor = brandExtractor;
        _logger = logger;
    }

    public async Task<ComparisonResult> CompareProductAsync(
        ImportedProduct importedProduct,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var normalizedBarcode = _barcodeNormalizer.Normalize(importedProduct.Barcode);
        var barcodeType = _barcodeNormalizer.DetectType(importedProduct.Barcode);
        
        OpenFoodFactsProduct? offProduct = null;
        string matchMethod = "none";
        double matchConfidence = 0;

        // Strategy 1: Multi-strategy barcode matching (only if it's a real barcode, not SKU)
        // Also require at least 8 digits to avoid false matches with internal store codes
        if (barcodeType != BarcodeType.Sku && barcodeType != BarcodeType.Unknown && normalizedBarcode.Length >= 8)
        {
            // Strategy 1a: Try exact normalized barcode match
            offProduct = await dbContext.OpenFoodFactsProducts
                .FirstOrDefaultAsync(p => p.NormalizedBarcode == normalizedBarcode, cancellationToken);
            
            if (offProduct != null)
            {
                matchMethod = "barcode_exact";
                matchConfidence = 1.0;
            }
            
            // Strategy 1b: If normalized is 12-digit UPC-A, also try as EAN-13 (prepend '0')
            if (offProduct == null && normalizedBarcode.Length == 12)
            {
                var ean13 = "0" + normalizedBarcode;
                offProduct = await dbContext.OpenFoodFactsProducts
                    .FirstOrDefaultAsync(p => p.Barcode == ean13 || p.NormalizedBarcode == ean13, cancellationToken);
                
                if (offProduct != null)
                {
                    matchMethod = "barcode_upc_to_ean13";
                    matchConfidence = 1.0;
                }
            }
            
            // Strategy 1c: Try original barcode (before normalization) in case it's already correct
            if (offProduct == null && importedProduct.Barcode != normalizedBarcode)
            {
                offProduct = await dbContext.OpenFoodFactsProducts
                    .FirstOrDefaultAsync(p => p.Barcode == importedProduct.Barcode, cancellationToken);
                
                if (offProduct != null)
                {
                    matchMethod = "barcode_original";
                    matchConfidence = 1.0;
                }
            }
        }

        // Strategy 2: Fallback to product name matching (if barcode is SKU or no match)
        if (offProduct == null && !string.IsNullOrWhiteSpace(importedProduct.ProductName))
        {
            // Normalize and expand abbreviations
            var normalizedName = _nameNormalizer.Normalize(importedProduct.ProductName);
            var searchTerm = normalizedName.ToLower().Trim();
            
            // Extract brand for better filtering
            var extractedBrand = _brandExtractor.ExtractBrand(importedProduct.ProductName);
            
            // Try exact name match first
            offProduct = await dbContext.OpenFoodFactsProducts
                .Where(p => p.ProductName != null && p.ProductName.ToLower() == searchTerm)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (offProduct != null)
            {
                matchMethod = "name_exact";
                matchConfidence = 0.85;
            }
            else
            {
                // Try partial name match with brand filter (fuzzy)
                var query = dbContext.OpenFoodFactsProducts
                    .Where(p => p.ProductName != null && EF.Functions.Like(p.ProductName.ToLower(), $"%{searchTerm}%"));
                
                // Filter by brand if extracted
                if (!string.IsNullOrEmpty(extractedBrand))
                {
                    query = query.Where(p => p.Brands != null && 
                        EF.Functions.Like(p.Brands.ToLower(), $"%{extractedBrand.ToLower()}%"));
                }
                
                var products = await query
                    .Take(10)
                    .ToListAsync(cancellationToken);
                
                // Find best match using advanced similarity
                offProduct = products
                    .Select(p => new { 
                        Product = p, 
                        Score = _nameNormalizer.CalculateAdvancedSimilarity(importedProduct.ProductName, p.ProductName ?? "") 
                    })
                    .Where(x => x.Score >= 0.65) // Slightly higher threshold with better normalization
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault()?.Product;
                
                if (offProduct != null)
                {
                    matchMethod = extractedBrand != null ? "name_fuzzy_brand" : "name_fuzzy";
                    matchConfidence = 0.7; // Higher confidence with brand matching
                }
            }
        }

        var result = new ComparisonResult
        {
            ImportedProductId = importedProduct.Id,
            ComparedAt = DateTime.UtcNow,
            HasNameDiscrepancy = false,
            HasBrandDiscrepancy = false,
            HasCategoryDiscrepancy = false,
            HasAllergenDiscrepancy = false,
            HasNutritionDiscrepancy = false
        };

        if (offProduct == null)
        {
            result.MatchStatus = MatchStatus.Unmatched;
            result.ConfidenceScore = 0;
            result.ComparisonDetails = JsonSerializer.Serialize(new { 
                reason = barcodeType == BarcodeType.Sku 
                    ? "Barcode appears to be internal SKU - no matches found in Open Food Facts" 
                    : "No match found in Open Food Facts",
                barcodeType = barcodeType.ToString(),
                searchedBarcode = importedProduct.Barcode,
                searchedName = importedProduct.ProductName
            });
        }
        else
        {
            result.OpenFoodFactsProductId = offProduct.Id;
            var discrepancies = CompareFields(importedProduct, offProduct);

            if (discrepancies.Count == 0)
            {
                result.MatchStatus = MatchStatus.Matched;
                result.ConfidenceScore = matchConfidence;
            }
            else
            {
                result.MatchStatus = MatchStatus.Discrepancy;
                result.ConfidenceScore = Math.Max(0.1, matchConfidence - (discrepancies.Count * 0.1));
            }

            result.HasNameDiscrepancy = discrepancies.ContainsKey("ProductName");
            result.HasBrandDiscrepancy = discrepancies.ContainsKey("Brand");
            result.HasCategoryDiscrepancy = discrepancies.ContainsKey("Category");
            result.HasAllergenDiscrepancy = discrepancies.ContainsKey("Allergens");

            result.ComparisonDetails = JsonSerializer.Serialize(new
            {
                matchMethod,
                matchConfidence,
                barcodeType = barcodeType.ToString(),
                matchedBarcode = offProduct.Barcode,
                offProductName = offProduct.ProductName,
                offBrand = offProduct.Brands,
                discrepancies,
                warning = barcodeType == BarcodeType.Sku ? "Match based on product name - original barcode is an internal SKU" : null
            });
        }

        return result;
    }
    
    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var normalizedA = a.ToLowerInvariant().Trim();
        var normalizedB = b.ToLowerInvariant().Trim();

        // Exact match
        if (normalizedA == normalizedB)
            return 1.0;

        // Contains match
        if (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA))
            return 0.8;

        // Word-based similarity
        var words1 = normalizedA.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = normalizedB.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }

    public async Task<List<ComparisonResult>> CompareBatchAsync(
        Guid batchId,
        string tenantId,
        int maxProducts = 500,
        string? searchFilter = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var query = dbContext.ImportedProducts
            .Where(p => p.TenantId == tenantId);
        
        // If batchId is Guid.Empty, compare ALL batches, otherwise filter by specific batch
        if (batchId != Guid.Empty)
        {
            query = query.Where(p => p.ImportBatchId == batchId);
        }

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            var filter = searchFilter.ToLower();
            query = query.Where(p =>
                p.Barcode.ToLower().Contains(filter) ||
                (p.ProductName != null && p.ProductName.ToLower().Contains(filter)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(filter)));
        }

        var products = await query
            .Take(maxProducts)
            .ToListAsync(cancellationToken);

        Console.WriteLine($"[COMPARISON] Found {products.Count} products to compare (batch: {(batchId == Guid.Empty ? "ALL" : batchId.ToString())}, max: {maxProducts}, filter: '{searchFilter}')");

        // OPTIMIZATION: Pre-load all potential OFF matches in ONE query
        var normalizedBarcodes = products
            .Select(p => _barcodeNormalizer.Normalize(p.Barcode))
            .Where(b => !string.IsNullOrEmpty(b))
            .Distinct()
            .ToList();
        
        // TEMPORARY: Also include legacy normalized barcodes (with FIRST leading zero removed)
        // This handles OFF products that haven't been re-normalized yet
        // Old buggy logic: 13-digit barcodes had the FIRST leading zero removed → 12 digits
        var legacyNormalizedBarcodes = normalizedBarcodes
            .Where(b => b.StartsWith("0") && b.Length == 13)
            .Select(b => b.Substring(1)) // Remove FIRST zero only, not all zeros
            .ToList();
        
        var allBarcodesToSearch = normalizedBarcodes.Concat(legacyNormalizedBarcodes).Distinct().ToList();

        Console.WriteLine($"[COMPARISON] Loading {normalizedBarcodes.Count} potential barcode matches (+{legacyNormalizedBarcodes.Count} legacy) from database...");
        var offProductsByBarcode = await dbContext.OpenFoodFactsProducts
            .Where(off => allBarcodesToSearch.Contains(off.NormalizedBarcode))
            .ToDictionaryAsync(off => off.NormalizedBarcode, cancellationToken);
        Console.WriteLine($"[COMPARISON] Loaded {offProductsByBarcode.Count} OFF products by barcode");

        // Pre-load product name matches for products without valid barcodes
        var productNamesForFuzzy = products
            .Where(p => !string.IsNullOrWhiteSpace(p.ProductName))
            .Select(p => p.ProductName!.ToLower())
            .Distinct()
            .ToList();

        Console.WriteLine($"[COMPARISON] Loading potential name matches for {productNamesForFuzzy.Count} unique product names...");
        var offProductsByName = new Dictionary<string, OpenFoodFactsProduct>();
        if (productNamesForFuzzy.Any())
        {
            var nameMatches = await dbContext.OpenFoodFactsProducts
                .Where(off => productNamesForFuzzy.Contains(off.ProductName.ToLower()))
                .ToListAsync(cancellationToken);
            
            foreach (var match in nameMatches)
            {
                var key = match.ProductName.ToLower();
                if (!offProductsByName.ContainsKey(key))
                {
                    offProductsByName[key] = match;
                }
            }
        }
        Console.WriteLine($"[COMPARISON] Loaded {offProductsByName.Count} OFF products by name");

        var results = new List<ComparisonResult>();
        var comparisonBatchId = Guid.NewGuid();
        int processed = 0;

        // Now process in memory - NO MORE DATABASE QUERIES!
        foreach (var product in products)
        {
            try
            {
                var result = await CompareProductInMemoryAsync(product, offProductsByBarcode, offProductsByName, cancellationToken);
                result.ComparisonBatchId = comparisonBatchId;
                results.Add(result);
                
                processed++;
                if (processed % 100 == 0)
                {
                    Console.WriteLine($"[COMPARISON] Processed {processed}/{products.Count} products ({(processed * 100.0 / products.Count):F1}%)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compare product {ProductId}", product.Id);
                results.Add(new ComparisonResult
                {
                    ImportedProductId = product.Id,
                    MatchStatus = MatchStatus.Error,
                    ComparisonBatchId = comparisonBatchId,
                    ComparisonDetails = JsonSerializer.Serialize(new { error = ex.Message }),
                    HasNameDiscrepancy = false,
                    HasBrandDiscrepancy = false,
                    HasCategoryDiscrepancy = false,
                    HasAllergenDiscrepancy = false,
                    HasNutritionDiscrepancy = false
                });
            }
        }

        Console.WriteLine($"[COMPARISON] Comparison complete. Saving {results.Count} results to database...");

        // Save results - use a new context to avoid disposal issues
        await using var saveContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // Check for existing comparison results to avoid duplicates
        var importedProductIds = results.Select(r => r.ImportedProductId).ToList();
        var existingResults = await saveContext.ComparisonResults
            .Where(cr => importedProductIds.Contains(cr.ImportedProductId))
            .Select(cr => cr.ImportedProductId)
            .ToListAsync(cancellationToken);
        
        // Only add new results that don't already exist
        var newResults = results
            .Where(r => !existingResults.Contains(r.ImportedProductId))
            .ToList();
        
        if (newResults.Any())
        {
            saveContext.ComparisonResults.AddRange(newResults);
            await saveContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Saved {NewCount} new comparison results for batch {BatchId} ({SkippedCount} duplicates skipped)", 
                newResults.Count, batchId, results.Count - newResults.Count);
        }
        else
        {
            _logger.LogInformation("All {Count} comparison results already exist for batch {BatchId}, skipped saving duplicates", 
                results.Count, batchId);
        }

        Console.WriteLine($"[COMPARISON] Results saved successfully!");

        return results;
    }

    /// <summary>
    /// Compare a product using pre-loaded OFF data (no database queries)
    /// </summary>
    private async Task<ComparisonResult> CompareProductInMemoryAsync(
        ImportedProduct importedProduct,
        Dictionary<string, OpenFoodFactsProduct> offProductsByBarcode,
        Dictionary<string, OpenFoodFactsProduct> offProductsByName,
        CancellationToken cancellationToken = default)
    {
        var normalizedBarcode = _barcodeNormalizer.Normalize(importedProduct.Barcode);
        var barcodeType = _barcodeNormalizer.DetectType(importedProduct.Barcode);

        OpenFoodFactsProduct? offProduct = null;
        string matchMethod = "Unmatched";
        double confidence = 0;
        string matchReason = "No match found";
        bool usedSecondarySearch = false;
        string? secondarySearchMethod = null;

        // Tier 1: Exact Barcode Match (only if it's a valid barcode type AND at least 8 digits)
        // Short codes (< 8 digits) are internal store SKUs, not real UPC/EAN barcodes
        // Matching them would cause false positives (e.g., store code "62" matching random OFF product)
        if (barcodeType != BarcodeType.Sku && barcodeType != BarcodeType.Unknown && 
            !string.IsNullOrEmpty(normalizedBarcode) && normalizedBarcode.Length >= 8)
        {
            if (offProductsByBarcode.TryGetValue(normalizedBarcode, out offProduct))
            {
                matchMethod = "BarcodeExact";
                confidence = 1.0;
                matchReason = "Exact barcode match";
            }
            
            // TEMPORARY: Try with old buggy normalization (ONE leading zero removed) for backwards compatibility
            // This handles OFF products that haven't been re-normalized yet
            // Old buggy logic: 13-digit barcodes had the FIRST leading zero removed → 12 digits
            if (offProduct == null && normalizedBarcode.StartsWith("0") && normalizedBarcode.Length == 13)
            {
                var legacyBarcode = normalizedBarcode.Substring(1); // Remove FIRST zero only, not all zeros
                if (offProductsByBarcode.TryGetValue(legacyBarcode, out offProduct))
                {
                    matchMethod = "BarcodeExact_Legacy";
                    confidence = 1.0;
                    matchReason = "Exact barcode match (legacy normalization)";
                }
            }
        }

        // Tier 2: Exact Product Name Match (if no barcode match or barcode is SKU)
        if (offProduct == null && !string.IsNullOrWhiteSpace(importedProduct.ProductName))
        {
            var productNameLower = importedProduct.ProductName.ToLower();
            if (offProductsByName.TryGetValue(productNameLower, out offProduct))
            {
                matchMethod = "ProductNameExact";
                confidence = 0.85;
                matchReason = "Exact product name match";
            }
        }

        // Secondary Search: Trigger if confidence < 100% (< 1.0)
        if (confidence < 1.0)
        {
            var secondaryResult = await PerformSecondarySearchAsync(
                importedProduct,
                offProductsByBarcode,
                offProductsByName,
                cancellationToken);

            if (secondaryResult.product != null && secondaryResult.confidence > confidence)
            {
                offProduct = secondaryResult.product;
                matchMethod = secondaryResult.method;
                confidence = secondaryResult.confidence;
                matchReason = secondaryResult.reason;
                usedSecondarySearch = true;
                secondarySearchMethod = secondaryResult.method;
            }
        }

        var result = new ComparisonResult
        {
            ImportedProductId = importedProduct.Id,
            ComparedAt = DateTime.UtcNow,
            ConfidenceScore = confidence,
            UsedSecondarySearch = usedSecondarySearch,
            SecondarySearchMethod = secondarySearchMethod,
            HasNameDiscrepancy = false,
            HasBrandDiscrepancy = false,
            HasCategoryDiscrepancy = false,
            HasAllergenDiscrepancy = false,
            HasNutritionDiscrepancy = false
        };

        if (offProduct != null)
        {
            result.OpenFoodFactsProductId = offProduct.Id;
            result.MatchStatus = MatchStatus.Matched;

            var discrepancies = CompareFields(importedProduct, offProduct);
            
            // Only mark as discrepancy if confidence < 100% (not exact barcode match)
            // Barcode is the source of truth - if it matches 100%, it's the same product
            if (discrepancies.Any() && confidence < 1.0)
            {
                result.MatchStatus = MatchStatus.Discrepancy;
                discrepancies["matchMethod"] = matchMethod;
                discrepancies["matchReason"] = matchReason;
                if (usedSecondarySearch)
                {
                    discrepancies["secondarySearchUsed"] = true;
                    discrepancies["secondarySearchMethod"] = secondarySearchMethod;
                }
                result.ComparisonDetails = JsonSerializer.Serialize(discrepancies);
            }
            else if (discrepancies.Any())
            {
                // Barcode match (100%) - keep as MATCHED but store informational differences
                var details = new Dictionary<string, object>
                {
                    ["matchMethod"] = matchMethod,
                    ["matchReason"] = matchReason,
                    ["informationalDifferences"] = discrepancies
                };
                if (usedSecondarySearch)
                {
                    details["secondarySearchUsed"] = true;
                    details["secondarySearchMethod"] = secondarySearchMethod;
                }
                result.ComparisonDetails = JsonSerializer.Serialize(details);
            }
            else
            {
                // Perfect match - no differences at all
                var details = new Dictionary<string, object>
                {
                    ["matchMethod"] = matchMethod,
                    ["matchReason"] = matchReason
                };
                if (usedSecondarySearch)
                {
                    details["secondarySearchUsed"] = true;
                    details["secondarySearchMethod"] = secondarySearchMethod;
                }
                result.ComparisonDetails = JsonSerializer.Serialize(details);
            }
        }
        else
        {
            result.MatchStatus = MatchStatus.Unmatched;
            result.ComparisonDetails = JsonSerializer.Serialize(new
            {
                matchMethod,
                matchReason,
                info = barcodeType == BarcodeType.Sku ? "Internal SKU detected - no barcode match available" : "No match found in Open Food Facts database",
                secondarySearchAttempted = usedSecondarySearch
            });
        }

        return result;
    }

    /// <summary>
    /// Perform secondary search using multiple strategies
    /// </summary>
    private async Task<(OpenFoodFactsProduct? product, string method, double confidence, string reason)> 
        PerformSecondarySearchAsync(
            ImportedProduct importedProduct,
            Dictionary<string, OpenFoodFactsProduct> offProductsByBarcode,
            Dictionary<string, OpenFoodFactsProduct> offProductsByName,
            CancellationToken cancellationToken)
    {
        OpenFoodFactsProduct? bestMatch = null;
        string bestMethod = "";
        double bestConfidence = 0;
        string bestReason = "";

        var normalizedBarcode = _barcodeNormalizer.Normalize(importedProduct.Barcode);

        // Strategy 1: Partial Barcode Matching (try last 8 digits)
        if (string.IsNullOrEmpty(bestMethod) && normalizedBarcode.Length >= 8)
        {
            var partialBarcode = normalizedBarcode.Substring(normalizedBarcode.Length - 8);
            var partialMatch = offProductsByBarcode.Values
                .FirstOrDefault(p => p.NormalizedBarcode.EndsWith(partialBarcode));

            if (partialMatch != null)
            {
                bestMatch = partialMatch;
                bestMethod = "BarcodePartial";
                bestConfidence = 0.75;
                bestReason = "Partial barcode match (last 8 digits)";
            }
        }

        // Strategy 2: AI-Normalized Name + Brand
        if (!string.IsNullOrWhiteSpace(importedProduct.ProductName))
        {
            try
            {
                var normalizedName = await _aiService.NormalizeProductNameAsync(
                    importedProduct.ProductName, cancellationToken);

                // Search with normalized name
                var candidates = offProductsByName.Values
                    .Where(p => !string.IsNullOrEmpty(p.ProductName) && 
                               FuzzyMatch(normalizedName, p.ProductName))
                    .ToList();

                // If brand available, prefer matches with same brand
                if (!string.IsNullOrWhiteSpace(importedProduct.Brand) && candidates.Any())
                {
                    var brandMatch = candidates
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p.Brands) && 
                                           FuzzyMatch(importedProduct.Brand, p.Brands));

                    if (brandMatch != null && 0.85 > bestConfidence)
                    {
                        bestMatch = brandMatch;
                        bestMethod = "AINameNormalized_Brand";
                        bestConfidence = 0.85;
                        bestReason = "AI-normalized name + brand match";
                    }
                }
                // Try just normalized name if no brand match - RE-ENABLED with improved safeguards
                // Uses actual similarity score instead of fixed confidence, with 0.70 threshold
                else if (candidates.Any() && bestConfidence < 0.70)
                {
                    // Calculate similarity scores for all candidates
                    var scoredCandidates = candidates
                        .Select(p => new {
                            Product = p,
                            Score = CalculateSimilarity(normalizedName, p.ProductName ?? "")
                        })
                        .Where(x => x.Score >= 0.70)  // Raised threshold for reliability
                        .Where(x => !IsGenericProductName(normalizedName))  // Exclude generic names
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    var bestCandidate = scoredCandidates.FirstOrDefault();
                    if (bestCandidate != null && bestCandidate.Score > bestConfidence)
                    {
                        bestMatch = bestCandidate.Product;
                        bestMethod = "AINameNormalized";
                        bestConfidence = bestCandidate.Score * 0.85;  // Scale confidence (max 0.85)
                        bestReason = $"AI-normalized name match ({bestCandidate.Score:P0} similarity)";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI normalization failed for product {ProductName}", importedProduct.ProductName);
            }
        }

        // Strategy 3: Multi-field Matching (Brand + Category + Size)
        if (!string.IsNullOrWhiteSpace(importedProduct.Brand))
        {
            var brandLower = importedProduct.Brand.ToLower();
            var candidates = offProductsByName.Values
                .Where(p => !string.IsNullOrEmpty(p.Brands) && 
                           p.Brands.ToLower().Contains(brandLower))
                .ToList();

            // Further filter by category if available
            if (!string.IsNullOrWhiteSpace(importedProduct.Category))
            {
                var categoryLower = importedProduct.Category.ToLower();
                candidates = candidates
                    .Where(p => !string.IsNullOrEmpty(p.Categories) && 
                               p.Categories.ToLower().Contains(categoryLower))
                    .ToList();
            }

            // Score all candidates - raised threshold from 0.65 to 0.85
            var scoredCandidates = candidates
                .Select(c => new {
                    Product = c,
                    Score = CalculateMultiFieldScore(importedProduct, c)
                })
                .Where(x => x.Score >= 0.85)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (scoredCandidates.Any() && scoredCandidates.First().Score > bestConfidence)
            {
                bestMatch = scoredCandidates.First().Product;
                bestConfidence = scoredCandidates.First().Score;
                bestMethod = "MultiField";
                bestReason = $"Brand + Category + Size match (score: {bestConfidence:P0})";
            }
        }

        return (bestMatch, bestMethod, bestConfidence, bestReason);
    }

    private static Dictionary<string, object> CompareFields(ImportedProduct imported, OpenFoodFactsProduct off)
    {
        var discrepancies = new Dictionary<string, object>();

        // Compare product name
        if (!string.IsNullOrEmpty(imported.ProductName) && !string.IsNullOrEmpty(off.ProductName))
        {
            if (!FuzzyMatch(imported.ProductName, off.ProductName))
            {
                discrepancies["ProductName"] = new
                {
                    imported = imported.ProductName,
                    openFoodFacts = off.ProductName
                };
            }
        }

        // Compare brand
        if (!string.IsNullOrEmpty(imported.Brand) && !string.IsNullOrEmpty(off.Brands))
        {
            if (!FuzzyMatch(imported.Brand, off.Brands))
            {
                discrepancies["Brand"] = new
                {
                    imported = imported.Brand,
                    openFoodFacts = off.Brands
                };
            }
        }

        // Compare category
        if (!string.IsNullOrEmpty(imported.Category) && !string.IsNullOrEmpty(off.Categories))
        {
            if (!off.Categories.ToLowerInvariant().Contains(imported.Category.ToLowerInvariant()))
            {
                discrepancies["Category"] = new
                {
                    imported = imported.Category,
                    openFoodFacts = off.Categories
                };
            }
        }

        // Compare allergens
        if (!string.IsNullOrEmpty(imported.Allergens) && !string.IsNullOrEmpty(off.Allergens))
        {
            if (!FuzzyMatch(imported.Allergens, off.Allergens))
            {
                discrepancies["Allergens"] = new
                {
                    imported = imported.Allergens,
                    openFoodFacts = off.Allergens
                };
            }
        }

        return discrepancies;
    }

    private static bool FuzzyMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        var normalizedA = a.ToLowerInvariant().Trim();
        var normalizedB = b.ToLowerInvariant().Trim();

        // Exact match
        if (normalizedA == normalizedB)
            return true;

        // Contains match
        if (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA))
            return true;

        // Calculate similarity (simplified Levenshtein-like)
        var words1 = normalizedA.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = normalizedB.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 && (double)commonWords / totalWords >= 0.5;
    }

    /// <summary>
    /// Checks if a product name is too generic for reliable name-only matching.
    /// Generic names are more likely to produce false positive matches.
    /// </summary>
    private static bool IsGenericProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return true;

        var normalized = productName.ToLowerInvariant().Trim();

        // Too short - likely to match incorrectly
        if (normalized.Length < 8)
            return true;

        // Exact matches for invalid/non-product values
        var invalidExactValues = new HashSet<string>
        {
            "no", "yes", "n/a", "na", "null", "none", "ea", "each",
            "item", "product", "misc", "other", "unknown", "tbd"
        };
        
        if (invalidExactValues.Contains(normalized))
            return true;

        // Common generic/open item names that shouldn't match
        var genericPatterns = new[]
        {
            "open item", "open ", "misc ", "unknown", "assorted",
            "generic", "store brand", "house brand", "private label",
            "sample", "test", "placeholder", "tbd", "n/a"
        };

        if (genericPatterns.Any(p => normalized.Contains(p)))
            return true;

        // Single common words that are too generic
        var genericSingleWords = new HashSet<string>
        {
            "bread", "milk", "eggs", "butter", "cheese", "water", "juice",
            "coffee", "tea", "sugar", "salt", "flour", "rice", "pasta",
            "chicken", "beef", "pork", "fish", "salad", "soup", "pizza"
        };

        // If the entire name is just one generic word, it's too generic
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1 && genericSingleWords.Contains(words[0]))
            return true;

        return false;
    }

    /// <summary>
    /// Calculate multi-field matching score for secondary search.
    /// </summary>
    private static double CalculateMultiFieldScore(ImportedProduct imported, OpenFoodFactsProduct off)
    {
        double score = 0;
        int fields = 0;

        // Brand match (30%)
        if (!string.IsNullOrEmpty(imported.Brand) && !string.IsNullOrEmpty(off.Brands))
        {
            if (FuzzyMatch(imported.Brand, off.Brands)) score += 0.3;
            fields++;
        }

        // Category match (25%)
        if (!string.IsNullOrEmpty(imported.Category) && !string.IsNullOrEmpty(off.Categories))
        {
            if (off.Categories.ToLower().Contains(imported.Category.ToLower())) score += 0.25;
            fields++;
        }

        // Quantity/Size match (20%)
        if (imported.Quantity.HasValue && !string.IsNullOrEmpty(off.Quantity))
        {
            if (QuantityMatches(imported.Quantity.Value, imported.UnitOfMeasure, off.Quantity))
                score += 0.20;
            fields++;
        }

        // Product name similarity (25%)
        if (!string.IsNullOrEmpty(imported.ProductName) && !string.IsNullOrEmpty(off.ProductName))
        {
            score += CalculateSimilarity(imported.ProductName, off.ProductName) * 0.25;
            fields++;
        }

        return fields > 0 ? score : 0;
    }

    /// <summary>
    /// Check if quantities match, handling unit conversions.
    /// </summary>
    private static bool QuantityMatches(decimal importedQty, string? unit, string offQuantity)
    {
        try
        {
            // Parse OFF quantity string (e.g., "16 oz", "500ml", "1 lb")
            var offQtyMatch = System.Text.RegularExpressions.Regex.Match(
                offQuantity, 
                @"(\d+\.?\d*)\s*([a-z]+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (!offQtyMatch.Success) return false;

            if (!decimal.TryParse(offQtyMatch.Groups[1].Value, out var offValue))
                return false;

            var offUnit = offQtyMatch.Groups[2].Value.ToLower();

            // Normalize both quantities to a common unit and compare
            var normalizedImported = NormalizeQuantity(importedQty, unit);
            var normalizedOff = NormalizeQuantity(offValue, offUnit);

            // Allow 5% tolerance for rounding differences
            if (normalizedImported == 0 || normalizedOff == 0) return false;
            
            var diff = Math.Abs(normalizedImported - normalizedOff);
            var tolerance = normalizedImported * 0.05m;

            return diff <= tolerance;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalize quantity to grams for weight or milliliters for volume.
    /// </summary>
    private static decimal NormalizeQuantity(decimal value, string? unit)
    {
        if (string.IsNullOrEmpty(unit))
            return value;

        var unitLower = unit.ToLower().Trim();

        // Weight conversions to grams
        return unitLower switch
        {
            "g" or "gr" or "gram" or "grams" => value,
            "kg" or "kilo" or "kilogram" or "kilograms" => value * 1000,
            "oz" or "ounce" or "ounces" => value * 28.3495m,
            "lb" or "lbs" or "pound" or "pounds" => value * 453.592m,
            
            // Volume conversions to milliliters
            "ml" or "milliliter" or "milliliters" => value,
            "l" or "liter" or "liters" => value * 1000,
            "fl oz" or "floz" or "fluid ounce" or "fluid ounces" => value * 29.5735m,
            "cup" or "cups" => value * 236.588m,
            "pt" or "pint" or "pints" => value * 473.176m,
            "qt" or "quart" or "quarts" => value * 946.353m,
            "gal" or "gallon" or "gallons" => value * 3785.41m,
            
            // Count/units (no conversion)
            "ct" or "count" or "units" or "pcs" or "pieces" => value,
            
            _ => value // Unknown unit, return as-is
        };
    }
}
