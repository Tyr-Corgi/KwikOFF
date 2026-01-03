using ClosedXML.Excel;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Excel file reader using ClosedXML.
/// </summary>
public class ExcelProductReader : IExcelProductReader
{
    public Task<(List<string> Headers, List<List<string>> Rows)> ReadHeadersAndSamplesAsync(Stream stream, int maxRows = 100)
    {
        return Task.Run(() => ReadFromStream(stream, maxRows));
    }

    public Task<(List<string> Headers, List<List<string>> Rows)> ReadAllAsync(Stream stream)
    {
        return Task.Run(() => ReadFromStream(stream, int.MaxValue));
    }

    public Task<List<string>> GetSheetNamesAsync(Stream stream)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook(stream);
            return workbook.Worksheets.Select(ws => ws.Name).ToList();
        });
    }

    private static (List<string> Headers, List<List<string>> Rows) ReadFromStream(Stream stream, int maxRows)
    {
        var headers = new List<string>();
        var rows = new List<List<string>>();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
            return (headers, rows);

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
            return (headers, rows);

        var firstRow = usedRange.FirstRow();
        var lastColumn = usedRange.LastColumn().ColumnNumber();

        // Read headers from first row
        for (int col = 1; col <= lastColumn; col++)
        {
            var cellValue = worksheet.Cell(1, col).GetString();
            headers.Add(string.IsNullOrWhiteSpace(cellValue) ? $"Column{col}" : cellValue);
        }

        // Read data rows
        var lastRow = Math.Min(usedRange.LastRow().RowNumber(), 1 + maxRows);
        for (int row = 2; row <= lastRow; row++)
        {
            var rowData = new List<string>();
            bool hasData = false;

            for (int col = 1; col <= lastColumn; col++)
            {
                var cell = worksheet.Cell(row, col);
                var value = GetCellValue(cell);
                rowData.Add(value);
                if (!string.IsNullOrWhiteSpace(value))
                    hasData = true;
            }

            // Only add rows that have at least some data
            if (hasData)
                rows.Add(rowData);
        }

        return (headers, rows);
    }

    private static string GetCellValue(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        // Handle different value types appropriately
        if (cell.DataType == XLDataType.DateTime)
        {
            try
            {
                return cell.GetDateTime().ToString("yyyy-MM-dd");
            }
            catch
            {
                return cell.GetString();
            }
        }

        if (cell.DataType == XLDataType.Number)
        {
            // Check if it looks like a barcode (long integer)
            var value = cell.GetDouble();
            if (value == Math.Floor(value) && value >= 10000000 && value <= 99999999999999)
            {
                return ((long)value).ToString();
            }
            return value.ToString();
        }

        return cell.GetString();
    }
}
