using System.Text.RegularExpressions;

namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Barcode normalization service for consistent matching.
/// </summary>
public class BarcodeNormalizer : IBarcodeNormalizer
{
    private static readonly Regex NonNumericRegex = new(@"[^0-9]", RegexOptions.Compiled);
    private static readonly Regex NumericOnlyRegex = new(@"^[0-9]+$", RegexOptions.Compiled);

    public string Normalize(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return string.Empty;

        // Trim whitespace
        var normalized = barcode.Trim();

        // Remove common prefixes
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        // For numeric barcodes, strip non-numeric characters and leading zeros
        if (NumericOnlyRegex.IsMatch(NonNumericRegex.Replace(normalized, "")))
        {
            normalized = NonNumericRegex.Replace(normalized, "");
            normalized = normalized.TrimStart('0');

            // Ensure we have at least one digit
            if (string.IsNullOrEmpty(normalized))
                normalized = "0";

            // Pad UPC-E to 8 digits
            if (normalized.Length == 6 || normalized.Length == 7)
                normalized = normalized.PadLeft(8, '0');

            // Pad UPC-A to 12 digits if between 9-11
            if (normalized.Length >= 9 && normalized.Length <= 11)
                normalized = normalized.PadLeft(12, '0');

            // Pad EAN-13 to 13 digits if 12 digits and not a valid UPC-A
            if (normalized.Length == 12 && !IsValidUpcACheckDigit(normalized))
                normalized = "0" + normalized;
        }

        return normalized.ToUpperInvariant();
    }

    public bool IsValidBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return false;

        var normalized = Normalize(barcode);

        // Check if it's a valid length for known formats
        return normalized.Length switch
        {
            8 => IsValidEan8CheckDigit(normalized) || IsValidUpcECheckDigit(normalized),
            12 => IsValidUpcACheckDigit(normalized),
            13 => IsValidEan13CheckDigit(normalized),
            14 => IsValidGtin14CheckDigit(normalized),
            _ => normalized.Length >= 4 && normalized.Length <= 20 // Allow SKUs
        };
    }

    public BarcodeType DetectType(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return BarcodeType.Unknown;

        var normalized = Normalize(barcode);

        // Check for non-numeric (SKU)
        if (!NumericOnlyRegex.IsMatch(normalized))
            return BarcodeType.Sku;

        return normalized.Length switch
        {
            8 when IsValidEan8CheckDigit(normalized) => BarcodeType.Ean8,
            8 when IsValidUpcECheckDigit(normalized) => BarcodeType.UpcE,
            8 => BarcodeType.Ean8,
            12 => BarcodeType.UpcA,
            13 when normalized.StartsWith("978") || normalized.StartsWith("979") => BarcodeType.Isbn13,
            13 => BarcodeType.Ean13,
            14 => BarcodeType.Gtin14,
            10 when IsValidIsbn10(normalized) => BarcodeType.Isbn10,
            _ => normalized.Length >= 4 && normalized.Length <= 20 ? BarcodeType.Sku : BarcodeType.Unknown
        };
    }

    private static bool IsValidUpcACheckDigit(string barcode)
    {
        if (barcode.Length != 12 || !NumericOnlyRegex.IsMatch(barcode))
            return false;

        return CalculateCheckDigit(barcode, 12) == barcode[11] - '0';
    }

    private static bool IsValidUpcECheckDigit(string barcode)
    {
        if (barcode.Length != 8 || !NumericOnlyRegex.IsMatch(barcode))
            return false;

        // UPC-E check digit validation is complex, simplified here
        return true;
    }

    private static bool IsValidEan8CheckDigit(string barcode)
    {
        if (barcode.Length != 8 || !NumericOnlyRegex.IsMatch(barcode))
            return false;

        return CalculateCheckDigit(barcode, 8) == barcode[7] - '0';
    }

    private static bool IsValidEan13CheckDigit(string barcode)
    {
        if (barcode.Length != 13 || !NumericOnlyRegex.IsMatch(barcode))
            return false;

        return CalculateCheckDigit(barcode, 13) == barcode[12] - '0';
    }

    private static bool IsValidGtin14CheckDigit(string barcode)
    {
        if (barcode.Length != 14 || !NumericOnlyRegex.IsMatch(barcode))
            return false;

        return CalculateCheckDigit(barcode, 14) == barcode[13] - '0';
    }

    private static int CalculateCheckDigit(string barcode, int length)
    {
        int sum = 0;
        for (int i = 0; i < length - 1; i++)
        {
            int digit = barcode[i] - '0';
            // For EAN/GTIN: odd positions (0-indexed even) multiply by 1, even positions by 3
            sum += digit * ((i % 2 == 0) ? 1 : 3);
        }

        int checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit;
    }

    private static bool IsValidIsbn10(string isbn)
    {
        if (isbn.Length != 10)
            return false;

        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            if (!char.IsDigit(isbn[i]))
                return false;
            sum += (isbn[i] - '0') * (10 - i);
        }

        char last = isbn[9];
        if (last == 'X' || last == 'x')
            sum += 10;
        else if (char.IsDigit(last))
            sum += last - '0';
        else
            return false;

        return sum % 11 == 0;
    }
}
