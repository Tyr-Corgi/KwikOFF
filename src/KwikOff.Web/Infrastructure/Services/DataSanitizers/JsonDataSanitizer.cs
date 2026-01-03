using System.Text.Json;
using System.Text.RegularExpressions;

namespace KwikOff.Web.Infrastructure.Services.DataSanitizers;

/// <summary>
/// Sanitizes crowdsourced data to handle common issues:
/// - Null bytes, control characters
/// - Invalid timestamps, unrealistic values
/// - Excessive field lengths
/// </summary>
public static class JsonDataSanitizer
{
    private static readonly Regex ControlCharsRegex = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);

    public static string? GetString(JsonElement root, string path, int maxLength = 10000)
    {
        try
        {
            var parts = path.Split('.');
            var current = root;

            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                    current = next;
                else
                    return null;
            }

            var value = current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
            
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            // Sanitize crowdsourced data issues:
            // 1. Remove null bytes (PostgreSQL doesn't allow them)
            value = value.Replace("\0", "");
            
            // 2. Remove other problematic control characters (0x01-0x1F) except newlines, tabs, carriage returns
            value = ControlCharsRegex.Replace(value, "");
            
            // 3. Trim excessive whitespace
            value = value.Trim();
            
            // 4. Truncate to max field length to prevent edge cases
            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }
            
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public static decimal? GetDecimal(JsonElement root, string path, decimal minValue = -999999, decimal maxValue = 999999)
    {
        try
        {
            var str = GetString(root, path);
            if (string.IsNullOrWhiteSpace(str))
                return null;
                
            if (decimal.TryParse(str, out var result))
            {
                // Filter out invalid values from crowdsourced data
                if (result < minValue || result > maxValue)
                    return null;
                    
                return result;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static int? GetInt(JsonElement root, string path, int minValue = 0, int maxValue = 100000)
    {
        try
        {
            var str = GetString(root, path);
            if (string.IsNullOrWhiteSpace(str))
                return null;
                
            if (int.TryParse(str, out var result))
            {
                // Validate reasonable range
                if (result < minValue || result > maxValue)
                    return null;
                    
                return result;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetDateTime(JsonElement root, string path)
    {
        try
        {
            var str = GetString(root, path);
            if (string.IsNullOrWhiteSpace(str))
                return null;
                
            if (long.TryParse(str, out var timestamp))
            {
                // Filter out invalid timestamps (before 1970 or after 2100)
                if (timestamp < 0 || timestamp > 4102444800) // Jan 1 2100
                    return null;
                    
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string SanitizeJsonForStorage(string json, int maxLength = 50000)
    {
        // Remove null bytes and control characters from JSON before storing
        if (string.IsNullOrEmpty(json)) return json;
        
        json = json.Replace("\0", "");
        json = ControlCharsRegex.Replace(json, "");
        
        // Truncate extremely large JSON
        if (json.Length > maxLength)
        {
            json = json.Substring(0, maxLength);
        }
        
        return json;
    }
}


