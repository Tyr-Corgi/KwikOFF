namespace KwikOff.Web.Infrastructure.Services;

/// <summary>
/// Service for normalizing barcodes to a consistent format.
/// </summary>
public interface IBarcodeNormalizer
{
    /// <summary>
    /// Normalizes a barcode for consistent matching.
    /// </summary>
    string Normalize(string barcode);

    /// <summary>
    /// Validates if a string is a valid barcode format.
    /// </summary>
    bool IsValidBarcode(string barcode);

    /// <summary>
    /// Detects the barcode type (UPC, EAN, GTIN, etc.).
    /// </summary>
    BarcodeType DetectType(string barcode);
}

/// <summary>
/// Types of barcodes supported.
/// </summary>
public enum BarcodeType
{
    Unknown,
    UpcA,      // 12 digits
    UpcE,      // 8 digits (compressed UPC-A)
    Ean8,      // 8 digits
    Ean13,     // 13 digits
    Gtin14,    // 14 digits
    Isbn10,    // 10 digits
    Isbn13,    // 13 digits (EAN-13 starting with 978/979)
    Sku        // Internal SKU (alphanumeric)
}
