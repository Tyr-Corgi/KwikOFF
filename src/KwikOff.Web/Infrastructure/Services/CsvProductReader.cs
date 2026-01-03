using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// CSV/TSV file reader with automatic delimiter detection.
/// </summary>
public class CsvProductReader : ICsvProductReader
{
    private static readonly char[] PossibleDelimiters = { ',', '\t', ';', '|' };

    public async Task<(List<string> Headers, List<List<string>> Rows)> ReadHeadersAndSamplesAsync(Stream stream, int maxRows = 100)
    {
        // Copy to memory stream for multiple reads
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var delimiter = DetectDelimiter(memoryStream);
        memoryStream.Position = 0;

        return await ReadWithDelimiterAsync(memoryStream, delimiter, maxRows);
    }

    public async Task<(List<string> Headers, List<List<string>> Rows)> ReadAllAsync(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var delimiter = DetectDelimiter(memoryStream);
        memoryStream.Position = 0;

        return await ReadWithDelimiterAsync(memoryStream, delimiter, int.MaxValue);
    }

    public char DetectDelimiter(Stream stream)
    {
        var position = stream.Position;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        // Read first few lines to detect delimiter
        var lines = new List<string>();
        for (int i = 0; i < 10 && !reader.EndOfStream; i++)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }

        stream.Position = position;

        if (lines.Count == 0)
            return ',';

        // Count occurrences of each delimiter in the first line
        var firstLine = lines[0];
        var delimiterCounts = PossibleDelimiters
            .Select(d => new { Delimiter = d, Count = firstLine.Count(c => c == d) })
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();

        if (delimiterCounts.Count == 0)
            return ',';

        // Verify the delimiter produces consistent column counts
        foreach (var dc in delimiterCounts)
        {
            var counts = lines.Select(l => CountFields(l, dc.Delimiter)).ToList();
            if (counts.Distinct().Count() == 1 && counts[0] > 1)
            {
                return dc.Delimiter;
            }
        }

        return delimiterCounts[0].Delimiter;
    }

    private static int CountFields(string line, char delimiter)
    {
        bool inQuotes = false;
        int count = 1;

        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == delimiter && !inQuotes)
                count++;
        }

        return count;
    }

    private async Task<(List<string> Headers, List<List<string>> Rows)> ReadWithDelimiterAsync(
        Stream stream, char delimiter, int maxRows)
    {
        var headers = new List<string>();
        var rows = new List<List<string>>();

        var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        // Read headers
        await csv.ReadAsync();
        csv.ReadHeader();
        headers = csv.HeaderRecord?.ToList() ?? new List<string>();

        // Read data rows
        int rowCount = 0;
        while (await csv.ReadAsync() && rowCount < maxRows)
        {
            var row = new List<string>();
            for (int i = 0; i < headers.Count; i++)
            {
                try
                {
                    row.Add(csv.GetField(i) ?? string.Empty);
                }
                catch
                {
                    row.Add(string.Empty);
                }
            }
            rows.Add(row);
            rowCount++;
        }

        return (headers, rows);
    }
}
