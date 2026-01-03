using System.Text.Json;
using KwikOff.Web.Domain.Entities;
using KwikOff.Web.Domain.Interfaces;
using KwikOff.Web.Domain.ValueObjects;
using KwikOff.Web.Infrastructure.Data;
using KwikOff.Web.Infrastructure.Services.FieldDetectors;
using Microsoft.EntityFrameworkCore;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for intelligent column detection using the Strategy pattern.
/// </summary>
public class ColumnDetectionService : IColumnDetectionService
{
    private readonly AppDbContext _dbContext;
    private readonly ICsvProductReader _csvReader;
    private readonly IExcelProductReader _excelReader;
    private readonly IColumnMappingValidator _validator;
    private readonly IReadOnlyList<IFieldDetector> _detectors;

    public ColumnDetectionService(
        AppDbContext dbContext,
        ICsvProductReader csvReader,
        IExcelProductReader excelReader,
        IColumnMappingValidator validator)
    {
        _dbContext = dbContext;
        _csvReader = csvReader;
        _excelReader = excelReader;
        _validator = validator;

        // Register all field detectors
        _detectors = new List<IFieldDetector>
        {
            // Required fields
            new BarcodeFieldDetector(),
            new ProductNameFieldDetector(),

            // Recommended fields
            new BrandFieldDetector(),
            new CategoryFieldDetector(),

            // Optional fields
            new DescriptionFieldDetector(),
            new PriceFieldDetector(),
            new SalesPriceFieldDetector(),
            new InternalSkuFieldDetector(),
            new SupplierFieldDetector(),
            new AllergensFieldDetector(),
            new QuantityFieldDetector(),
            new UnitOfMeasureFieldDetector(),

            // FSMA 204 fields
            new TraceabilityLotCodeFieldDetector(),
            new OriginLocationFieldDetector(),
            new CurrentLocationFieldDetector(),
            new DestinationLocationFieldDetector(),
            new HarvestDateFieldDetector(),
            new PackDateFieldDetector(),
            new ShipDateFieldDetector(),
            new ReceiveDateFieldDetector(),
            new ExpirationDateFieldDetector(),
            new ReferenceDocumentTypeFieldDetector(),
            new ReferenceDocumentNumberFieldDetector()
        };
    }

    public async Task<ColumnDetectionResult> DetectColumnsAsync(Stream fileStream, string fileName, string tenantId)
    {
        // Check for saved mapping first
        var savedMapping = await GetSavedMappingAsync(tenantId, fileName);

        // Read file headers and sample data
        var (headers, sampleData) = await ReadFileAsync(fileStream, fileName);

        if (headers.Count == 0)
        {
            return new ColumnDetectionResult
            {
                Errors = new List<string> { "No columns detected in file" }
            };
        }

        // Detect columns
        var detectedColumns = new List<DetectedColumn>();
        for (int i = 0; i < headers.Count; i++)
        {
            var columnSamples = sampleData.Select(row =>
                row.Count > i ? row[i] : "").ToList();

            var suggestions = DetectFieldsForColumn(headers[i], columnSamples);

            detectedColumns.Add(new DetectedColumn
            {
                Index = i,
                HeaderName = headers[i],
                SampleValues = columnSamples.Take(5).ToList(),
                Suggestions = suggestions
            });
        }

        // Build mapping from best matches
        var mapping = savedMapping ?? BuildMappingFromDetections(detectedColumns);

        // Validate the mapping
        var validationResult = _validator.Validate(mapping);

        // Build sample data dictionary
        var sampleDataDict = sampleData.Take(5).Select(row =>
        {
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < headers.Count && i < row.Count; i++)
            {
                dict[headers[i]] = row[i];
            }
            return dict;
        }).ToList();

        // Calculate overall confidence
        var mappedFields = detectedColumns
            .Where(c => c.BestConfidence > 0)
            .ToList();
        var overallConfidence = mappedFields.Count > 0
            ? mappedFields.Average(c => c.BestConfidence)
            : 0;

        return new ColumnDetectionResult
        {
            DetectedMapping = mapping,
            AllColumns = detectedColumns,
            OverallConfidence = overallConfidence,
            Errors = validationResult.Errors,
            Warnings = validationResult.Warnings,
            SampleData = sampleDataDict,
            Headers = headers
        };
    }

    private List<FieldSuggestion> DetectFieldsForColumn(string headerName, IReadOnlyList<string> sampleValues)
    {
        var suggestions = new List<FieldSuggestion>();

        foreach (var detector in _detectors)
        {
            var result = detector.Detect(headerName, sampleValues);
            if (result.Confidence > 0)
            {
                suggestions.Add(new FieldSuggestion
                {
                    FieldName = result.FieldName,
                    DisplayName = result.DisplayName,
                    Confidence = result.Confidence,
                    Reason = result.Reason,
                    IsRequired = result.IsRequired,
                    IsFsma204 = result.IsFsma204
                });
            }
        }

        return suggestions.OrderByDescending(s => s.Confidence).ToList();
    }

    private ColumnMapping BuildMappingFromDetections(List<DetectedColumn> columns)
    {
        var mapping = new ColumnMapping();
        var usedFields = new HashSet<string>();

        // Sort by confidence to assign best matches first
        var sortedMatches = columns
            .Where(c => c.Suggestions.Count > 0)
            .SelectMany(c => c.Suggestions.Select(s => new { Column = c, Suggestion = s }))
            .OrderByDescending(x => x.Suggestion.Confidence)
            .ToList();

        foreach (var match in sortedMatches)
        {
            if (usedFields.Contains(match.Suggestion.FieldName))
                continue;

            var mapInfo = new ColumnMapInfo
            {
                ColumnIndex = match.Column.Index,
                ColumnName = match.Column.HeaderName,
                Confidence = match.Suggestion.Confidence,
                Reason = match.Suggestion.Reason
            };

            SetMappingField(mapping, match.Suggestion.FieldName, mapInfo);
            usedFields.Add(match.Suggestion.FieldName);
        }

        return mapping;
    }

    private static void SetMappingField(ColumnMapping mapping, string fieldName, ColumnMapInfo mapInfo)
    {
        switch (fieldName)
        {
            case "Barcode": mapping.Barcode = mapInfo; break;
            case "ProductName": mapping.ProductName = mapInfo; break;
            case "Brand": mapping.Brand = mapInfo; break;
            case "Category": mapping.Category = mapInfo; break;
            case "Description": mapping.Description = mapInfo; break;
            case "Price": mapping.Price = mapInfo; break;
            case "SalesPrice": mapping.SalesPrice = mapInfo; break;
            case "InternalSku": mapping.InternalSku = mapInfo; break;
            case "Supplier": mapping.Supplier = mapInfo; break;
            case "Allergens": mapping.Allergens = mapInfo; break;
            case "Quantity": mapping.Quantity = mapInfo; break;
            case "UnitOfMeasure": mapping.UnitOfMeasure = mapInfo; break;
            case "TraceabilityLotCode": mapping.TraceabilityLotCode = mapInfo; break;
            case "OriginLocation": mapping.OriginLocation = mapInfo; break;
            case "CurrentLocation": mapping.CurrentLocation = mapInfo; break;
            case "DestinationLocation": mapping.DestinationLocation = mapInfo; break;
            case "HarvestDate": mapping.HarvestDate = mapInfo; break;
            case "PackDate": mapping.PackDate = mapInfo; break;
            case "ShipDate": mapping.ShipDate = mapInfo; break;
            case "ReceiveDate": mapping.ReceiveDate = mapInfo; break;
            case "ExpirationDate": mapping.ExpirationDate = mapInfo; break;
            case "ReferenceDocumentType": mapping.ReferenceDocumentType = mapInfo; break;
            case "ReferenceDocumentNumber": mapping.ReferenceDocumentNumber = mapInfo; break;
        }
    }

    private async Task<(List<string> Headers, List<List<string>> Rows)> ReadFileAsync(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".tsv" => await _csvReader.ReadHeadersAndSamplesAsync(stream, 100),
            ".xlsx" or ".xls" => await _excelReader.ReadHeadersAndSamplesAsync(stream, 100),
            _ => (new List<string>(), new List<List<string>>())
        };
    }

    public async Task SaveColumnMappingAsync(string tenantId, string filePattern, ColumnMapping mapping, string? userId)
    {
        var existing = await _dbContext.TenantColumnMappings
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.FilePattern == filePattern);

        var json = JsonSerializer.Serialize(mapping);

        if (existing != null)
        {
            existing.ColumnMappingJson = json;
            existing.LastUsedAt = DateTime.UtcNow;
            existing.UseCount++;
        }
        else
        {
            _dbContext.TenantColumnMappings.Add(new TenantColumnMapping
            {
                TenantId = tenantId,
                FilePattern = filePattern,
                ColumnMappingJson = json,
                CreatedByUser = userId,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                UseCount = 1
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<ColumnMapping?> GetSavedMappingAsync(string tenantId, string fileName)
    {
        // Try exact match first
        var mapping = await _dbContext.TenantColumnMappings
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .OrderByDescending(m => m.LastUsedAt)
            .FirstOrDefaultAsync(m => m.FilePattern == fileName ||
                                       m.FilePattern == Path.GetExtension(fileName) ||
                                       m.FilePattern == "*" + Path.GetExtension(fileName));

        if (mapping == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ColumnMapping>(mapping.ColumnMappingJson);
        }
        catch
        {
            return null;
        }
    }
}
