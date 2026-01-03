namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Interface for reading CSV/TSV files.
/// </summary>
public interface ICsvProductReader
{
    /// <summary>
    /// Reads headers and sample data from a CSV file.
    /// </summary>
    Task<(List<string> Headers, List<List<string>> Rows)> ReadHeadersAndSamplesAsync(Stream stream, int maxRows = 100);

    /// <summary>
    /// Reads all data from a CSV file.
    /// </summary>
    Task<(List<string> Headers, List<List<string>> Rows)> ReadAllAsync(Stream stream);

    /// <summary>
    /// Detects the delimiter used in a CSV file.
    /// </summary>
    char DetectDelimiter(Stream stream);
}
