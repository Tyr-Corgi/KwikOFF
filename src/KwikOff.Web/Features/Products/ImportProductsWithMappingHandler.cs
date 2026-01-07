using System.Diagnostics;
using System.Text.Json;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Domain.ValueObjects;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Features.Products;

/// <summary>
/// Handler for importing products with dynamic column mapping.
/// </summary>
public class ImportProductsWithMappingHandler : IRequestHandler<ImportProductsWithMappingCommand, ImportProductsResult>
{
    private readonly AppDbContext _dbContext;
    private readonly ICsvProductReader _csvReader;
    private readonly IExcelProductReader _excelReader;
    private readonly IBarcodeNormalizer _barcodeNormalizer;
    private readonly IColumnDetectionService _columnDetectionService;
    private readonly ILogger<ImportProductsWithMappingHandler> _logger;

    private const int BatchSize = 1000;
    private int _duplicateCount = 0;

    public ImportProductsWithMappingHandler(
        AppDbContext dbContext,
        ICsvProductReader csvReader,
        IExcelProductReader excelReader,
        IBarcodeNormalizer barcodeNormalizer,
        IColumnDetectionService columnDetectionService,
        ILogger<ImportProductsWithMappingHandler> logger)
    {
        _dbContext = dbContext;
        _csvReader = csvReader;
        _excelReader = excelReader;
        _barcodeNormalizer = barcodeNormalizer;
        _columnDetectionService = columnDetectionService;
        _logger = logger;
    }

    public async Task<ImportProductsResult> Handle(
        ImportProductsWithMappingCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportProductsResult
        {
            BatchId = Guid.NewGuid(),
            Success = false
        };
        
        int skuCount = 0;
        int validBarcodeCount = 0;

        try
        {
            // Read file data
            var (headers, rows) = await ReadFileAsync(request.FileStream, request.FileName);

            if (headers.Count == 0 || rows.Count == 0)
            {
                result.Errors.Add("No data found in file");
                return result;
            }

            result.TotalRows = rows.Count;

            // Save mapping if requested
            if (request.SaveMapping)
            {
                var filePattern = "*" + Path.GetExtension(request.FileName);
                await _columnDetectionService.SaveColumnMappingAsync(
                    request.TenantId, filePattern, request.Mapping, request.UserId);
            }

            // Import in batches
            var products = new List<ImportedProduct>();
            int rowNumber = 1;

            foreach (var row in rows)
            {
                rowNumber++;
                try
                {
                    var product = MapRowToProduct(
                        headers, row, request.Mapping, request.TenantId,
                        result.BatchId, request.FileName, rowNumber);

                    if (product != null)
                    {
                        // Check barcode type for warnings
                        var barcodeType = _barcodeNormalizer.DetectType(product.Barcode);
                        if (barcodeType == BarcodeType.Sku || barcodeType == BarcodeType.Unknown)
                        {
                            skuCount++;
                        }
                        else
                        {
                            validBarcodeCount++;
                        }
                        
                        products.Add(product);
                        result.ImportedCount++;
                    }
                    else
                    {
                        result.SkippedCount++;
                    }

                    // Batch insert
                    if (products.Count >= BatchSize)
                    {
                        await SaveBatchAsync(products, cancellationToken);
                        products.Clear();
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    if (result.Errors.Count < 10)
                    {
                        result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                    }
                    _logger.LogWarning(ex, "Error importing row {RowNumber}", rowNumber);
                }
            }

            // Save remaining products
            if (products.Count > 0)
            {
                await SaveBatchAsync(products, cancellationToken);
            }

            result.Success = result.ImportedCount > 0;
            
            // Add duplicates to skipped count
            result.SkippedCount += _duplicateCount;
            
            // Add warning about duplicates
            if (_duplicateCount > 0)
            {
                result.Warnings.Add($"{_duplicateCount} duplicate products were skipped (barcode already exists for this tenant).");
            }
            
            // Add warnings about barcode types
            if (skuCount > 0)
            {
                var percentage = (int)((double)skuCount / result.ImportedCount * 100);
                result.Warnings.Add($"{skuCount} products ({percentage}%) have internal SKUs instead of standard barcodes (UPC/EAN). Comparison will use product name matching, which is less accurate.");
            }
            
            if (validBarcodeCount > 0 && skuCount > 0)
            {
                result.Warnings.Add($"Mix of barcodes detected: {validBarcodeCount} standard barcodes, {skuCount} SKUs. Consider using consistent product identifiers.");
            }

            _logger.LogInformation(
                "Imported {ImportedCount} products from {FileName} for tenant {TenantId}",
                result.ImportedCount, request.FileName, request.TenantId);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import failed: {ex.Message}");
            _logger.LogError(ex, "Failed to import products from {FileName}", request.FileName);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }

    private async Task<(List<string> Headers, List<List<string>> Rows)> ReadFileAsync(
        Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".tsv" => await _csvReader.ReadAllAsync(stream),
            ".xlsx" or ".xls" => await _excelReader.ReadAllAsync(stream),
            _ => throw new NotSupportedException($"File type {extension} is not supported")
        };
    }

    private ImportedProduct? MapRowToProduct(
        List<string> headers, List<string> row, ColumnMapping mapping,
        string tenantId, Guid batchId, string fileName, int rowNumber)
    {
        // Get required fields
        var barcode = GetValueFromMapping(row, mapping.Barcode);
        var productName = GetValueFromMapping(row, mapping.ProductName);

        // Skip rows without required data
        if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        // Build original data JSON
        var originalData = new Dictionary<string, string>();
        for (int i = 0; i < headers.Count && i < row.Count; i++)
        {
            originalData[headers[i]] = row[i];
        }

        var product = new ImportedProduct
        {
            Barcode = barcode.Trim(),
            NormalizedBarcode = _barcodeNormalizer.Normalize(barcode),
            ProductName = productName.Trim(),
            Brand = GetValueFromMapping(row, mapping.Brand),
            Category = GetValueFromMapping(row, mapping.Category),
            Description = GetValueFromMapping(row, mapping.Description),
            Supplier = GetValueFromMapping(row, mapping.Supplier),
            InternalSku = GetValueFromMapping(row, mapping.InternalSku),
            Allergens = GetValueFromMapping(row, mapping.Allergens),
            UnitOfMeasure = GetValueFromMapping(row, mapping.UnitOfMeasure),

            // Pricing
            Price = ParseDecimal(GetValueFromMapping(row, mapping.Price)),
            SalesPrice = ParseDecimal(GetValueFromMapping(row, mapping.SalesPrice)),
            Quantity = ParseDecimal(GetValueFromMapping(row, mapping.Quantity)),

            // FSMA 204 fields
            TraceabilityLotCode = GetValueFromMapping(row, mapping.TraceabilityLotCode),
            OriginLocation = GetValueFromMapping(row, mapping.OriginLocation),
            CurrentLocation = GetValueFromMapping(row, mapping.CurrentLocation),
            DestinationLocation = GetValueFromMapping(row, mapping.DestinationLocation),
            HarvestDate = ParseDate(GetValueFromMapping(row, mapping.HarvestDate)),
            PackDate = ParseDate(GetValueFromMapping(row, mapping.PackDate)),
            ShipDate = ParseDate(GetValueFromMapping(row, mapping.ShipDate)),
            ReceiveDate = ParseDate(GetValueFromMapping(row, mapping.ReceiveDate)),
            ExpirationDate = ParseDate(GetValueFromMapping(row, mapping.ExpirationDate)),
            ReferenceDocumentType = GetValueFromMapping(row, mapping.ReferenceDocumentType),
            ReferenceDocumentNumber = GetValueFromMapping(row, mapping.ReferenceDocumentNumber),

            // Metadata
            TenantId = tenantId,
            ImportBatchId = batchId,
            FileName = fileName,
            RowNumber = rowNumber,
            ImportedAt = DateTime.UtcNow,
            OriginalData = JsonSerializer.Serialize(originalData)
        };

        return product;
    }

    private static string? GetValueFromMapping(List<string> row, ColumnMapInfo? mapInfo)
    {
        if (mapInfo == null || mapInfo.ColumnIndex < 0 || mapInfo.ColumnIndex >= row.Count)
            return null;

        var value = row[mapInfo.ColumnIndex];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Remove currency symbols and commas
        var cleaned = value.Replace("$", "").Replace("€", "").Replace("£", "")
                          .Replace(",", "").Trim();

        return decimal.TryParse(cleaned, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private async Task SaveBatchAsync(List<ImportedProduct> products, CancellationToken cancellationToken)
    {
        // Check for existing products with same tenant_id + barcode
        var tenantId = products.First().TenantId;
        var barcodes = products.Select(p => p.Barcode).ToList();
        
        var existingBarcodes = await _dbContext.ImportedProducts
            .Where(p => p.TenantId == tenantId && barcodes.Contains(p.Barcode))
            .Select(p => p.Barcode)
            .ToListAsync(cancellationToken);
        
        // Filter out duplicates - only add products that don't already exist
        var newProducts = products
            .Where(p => !existingBarcodes.Contains(p.Barcode))
            .ToList();
        
        if (newProducts.Any())
        {
            _dbContext.ImportedProducts.AddRange(newProducts);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        
        // Log duplicates that were skipped
        var duplicateCount = products.Count - newProducts.Count;
        if (duplicateCount > 0)
        {
            _duplicateCount += duplicateCount;
            _logger.LogInformation(
                "Skipped {DuplicateCount} duplicate products for tenant {TenantId}",
                duplicateCount, tenantId);
        }
    }
}
