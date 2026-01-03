namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Interface for reading Excel files (.xlsx, .xls).
/// </summary>
public interface IExcelProductReader
{
    /// <summary>
    /// Reads headers and sample data from an Excel file.
    /// </summary>
    Task<(List<string> Headers, List<List<string>> Rows)> ReadHeadersAndSamplesAsync(Stream stream, int maxRows = 100);

    /// <summary>
    /// Reads all data from an Excel file.
    /// </summary>
    Task<(List<string> Headers, List<List<string>> Rows)> ReadAllAsync(Stream stream);

    /// <summary>
    /// Gets the sheet names from an Excel file.
    /// </summary>
    Task<List<string>> GetSheetNamesAsync(Stream stream);
}
