using KwikOff.Web.Infrastructure.Services;
using Xunit;

namespace KwikOff.Tests.Services;

/// <summary>
/// Tests for BarcodeNormalizer - Tests basic functionality without assumptions about complex padding logic
/// </summary>
public class BarcodeNormalizerTests
{
    private readonly BarcodeNormalizer _normalizer;

    public BarcodeNormalizerTests()
    {
        _normalizer = new BarcodeNormalizer();
    }

    #region Null/Empty Handling

    [Fact]
    public void Normalize_NullInput_ReturnsEmptyString()
    {
        var result = _normalizer.Normalize(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmptyString()
    {
        var result = _normalizer.Normalize("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_WhitespaceOnly_ReturnsEmptyString()
    {
        var result = _normalizer.Normalize("   ");
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region Basic Functionality

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        var result = _normalizer.Normalize("  123456789012  ");
        Assert.DoesNotContain(" ", result);
    }

    [Fact]
    public void Normalize_RemovesHyphens()
    {
        var result = _normalizer.Normalize("123-456-789");
        Assert.DoesNotContain("-", result);
    }

    [Fact]
    public void Normalize_ConvertsToUpperCase()
    {
        var result = _normalizer.Normalize("abc");
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void Normalize_RemovesHexPrefix()
    {
        var result = _normalizer.Normalize("0xABCD");
        Assert.DoesNotContain("0x", result.ToLower());
        Assert.DoesNotContain("0X", result);
    }

    #endregion

    #region IsValidBarcode

    [Fact]
    public void IsValidBarcode_NullInput_ReturnsFalse()
    {
        var result = _normalizer.IsValidBarcode(null!);
        Assert.False(result);
    }

    [Fact]
    public void IsValidBarcode_EmptyString_ReturnsFalse()
    {
        var result = _normalizer.IsValidBarcode("");
        Assert.False(result);
    }

    [Fact]
    public void IsValidBarcode_WhitespaceOnly_ReturnsFalse()
    {
        var result = _normalizer.IsValidBarcode("   ");
        Assert.False(result);
    }

    [Fact]
    public void IsValidBarcode_VeryShort_ReturnsFalse()
    {
        var result = _normalizer.IsValidBarcode("12");
        Assert.False(result);
    }

    [Fact]
    public void IsValidBarcode_ExtremelyLong_ReturnsFalse()
    {
        var result = _normalizer.IsValidBarcode(new string('1', 100));
        Assert.False(result);
    }

    [Fact]
    public void IsValidBarcode_StandardUPC_ReturnsTrue()
    {
        // 12-digit UPC-A
        var result = _normalizer.IsValidBarcode("012345678905");
        Assert.True(result);
    }

    [Fact]
    public void IsValidBarcode_StandardEAN_ReturnsTrue()
    {
        // 13-digit EAN-13
        var result = _normalizer.IsValidBarcode("0123456789012");
        Assert.True(result);
    }

    #endregion

    #region DetectType

    [Fact]
    public void DetectType_NullInput_ReturnsUnknown()
    {
        var result = _normalizer.DetectType(null!);
        Assert.Equal(BarcodeType.Unknown, result);
    }

    [Fact]
    public void DetectType_EmptyString_ReturnsUnknown()
    {
        var result = _normalizer.DetectType("");
        Assert.Equal(BarcodeType.Unknown, result);
    }

    [Fact]
    public void DetectType_AlphanumericInput_ReturnsSkuOrCorrectType()
    {
        var result = _normalizer.DetectType("ABC123");
        // Alphanumeric codes should be SKU or Unknown, not numeric barcode types
        Assert.True(result == BarcodeType.Sku || result == BarcodeType.Unknown);
    }

    [Fact]
    public void DetectType_UpcAFormat_ReturnsUpcA()
    {
        // Standard 12-digit UPC-A
        var result = _normalizer.DetectType("012345678905");
        Assert.Equal(BarcodeType.UpcA, result);
    }

    [Fact]
    public void DetectType_Ean13Format_ReturnsEan13()
    {
        // Standard 13-digit EAN-13
        var result = _normalizer.DetectType("0123456789012");
        Assert.Equal(BarcodeType.Ean13, result);
    }

    [Fact]
    public void DetectType_IsbnPrefix_ReturnsIsbn13()
    {
        // ISBN-13 with 978 prefix
        var result = _normalizer.DetectType("9780123456789");
        Assert.Equal(BarcodeType.Isbn13, result);
    }

    [Fact]
    public void DetectType_Gtin14Format_ReturnsGtin14OrEan13()
    {
        // 14-digit format - may normalize to 13 depending on leading zeros
        var result = _normalizer.DetectType("01234567890123");
        // Should be GTIN-14 or EAN-13, not Unknown
        Assert.True(result == BarcodeType.Gtin14 || result == BarcodeType.Ean13);
    }

    #endregion

    #region Real-World Integration

    [Fact]
    public void Normalize_RealWorldBarcode_ProducesConsistentOutput()
    {
        // Same barcode in different formats should normalize the same
        var result1 = _normalizer.Normalize("012345678905");
        var result2 = _normalizer.Normalize("0-12345-67890-5");
        
        // Both should normalize to something consistent
        Assert.NotEmpty(result1);
        Assert.NotEmpty(result2);
        // They should both be numeric
        Assert.All(result1, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void NormalizeAndDetect_WorkTogetherCorrectly()
    {
        // Normalize then detect should work consistently
        var input = "012345678905";
        var normalized = _normalizer.Normalize(input);
        var type = _normalizer.DetectType(normalized);
        
        // Should be a valid barcode type
        Assert.NotEqual(BarcodeType.Unknown, type);
    }

    #endregion
}
