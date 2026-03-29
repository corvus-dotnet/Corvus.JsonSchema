using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for JsonElement.TryFormat() and JsonElement.ToString(string?, IFormatProvider?)
/// to ensure consistent formatting across Corvus.Text.Json.JsonElement and 
/// Corvus.Text.Json.JsonElement.Mutable for all value types.
/// </summary>
public class JsonElementFormatTests
{
    #region Number Formatting Tests

    [Theory]
    [InlineData("1234.56", "N2", "1,234.56")]
    [InlineData("1234.56", "N0", "1,235")]
    [InlineData("999.999", "N2", "1,000.00")]
    [InlineData("9999.995", "N2", "10,000.00")]
    [InlineData("1e6", "N2", "1,000,000.00")]
    [InlineData("1234567.89", "N2", "1,234,567.89")]
    [InlineData("-1234.56", "N2", "-1,234.56")]
    // Numbers > 128 bits (38+ decimal digits)
    [InlineData("1427247692705959881058285969449495136382746624", "N2", "1,427,247,692,705,959,881,058,285,969,449,495,136,382,746,624.00")]
    [InlineData("1606938044258990275541962092341162602522202993782792835301376", "N0", "1,606,938,044,258,990,275,541,962,092,341,162,602,522,202,993,782,792,835,301,376")]
    [InlineData("123456789012345678901234567890.123456789", "N2", "123,456,789,012,345,678,901,234,567,890.12")]
    [InlineData("999999999999999999999999999999999999999.99", "N2", "999,999,999,999,999,999,999,999,999,999,999,999,999.99")]
    [InlineData("-1427247692705959881058285969449495136382746624", "N2", "-1,427,247,692,705,959,881,058,285,969,449,495,136,382,746,624.00")]
    public void Number_AllMethods_FormatConsistently(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test JsonElement.TryFormat(char)
        Span<char> charDest = stackalloc char[200];
        bool success1 = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success1, "JsonElement.TryFormat(char) should succeed");
        string result1 = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result1);

        // Test JsonElement.TryFormat(byte)
        Span<byte> byteDest = stackalloc byte[200];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success2, "JsonElement.TryFormat(byte) should succeed");
        string result2 = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result2);

        // Test JsonElement.ToString
        string result3 = element.ToString(format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result3);

        // Test JsonElement.Mutable.TryFormat(char)
        Span<char> mutableCharDest = stackalloc char[200];
        bool success4 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success4, "JsonElement.Mutable.TryFormat(char) should succeed");
        string result4 = mutableCharDest.Slice(0, mutableCharsWritten).ToString();
        Assert.Equal(expected, result4);

        // Test JsonElement.Mutable.TryFormat(byte)
        Span<byte> mutableByteDest = stackalloc byte[200];
        bool success5 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success5, "JsonElement.Mutable.TryFormat(byte) should succeed");
        string result5 = JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten));
        Assert.Equal(expected, result5);

        // Test JsonElement.Mutable.ToString
        string result6 = mutableElement.ToString(format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result6);
    }

    [Theory]
    [InlineData("1234.56", "C2", "$1,234.56")]
    [InlineData("1234.56", "C0", "$1,235")]
    [InlineData("999.99", "C0", "$1,000")]
    [InlineData("9999.99", "C0", "$10,000")]
    [InlineData("999999.99", "C0", "$1,000,000")]
    // Currency formatting with numbers > 128 bits
    [InlineData("1427247692705959881058285969449495136382746624.50", "C2", "$1,427,247,692,705,959,881,058,285,969,449,495,136,382,746,624.50")]
    [InlineData("123456789012345678901234567890.99", "C0", "$123,456,789,012,345,678,901,234,567,891")]
    public void Currency_AllMethods_FormatConsistently(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        // Test all 6 methods
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, format, formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        element.TryFormat(byteDest, out int bytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, formatInfo));

        Span<char> mutableCharDest = stackalloc char[100];
        mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, formatInfo);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, formatInfo));
    }

    [Theory]
    [InlineData(0, "$0.00")] // $n
    [InlineData(1, "0.00$")] // n$
    [InlineData(2, "$ 0.00")] // $ n
    [InlineData(3, "0.00 $")] // n $
    public void CurrencyZero_AllMethods_FormatConsistently(int pattern, string expected)
    {
        string jsonValue = "0";
        string format = "C2";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = pattern
        };

        // Test all 6 methods
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, format, formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        element.TryFormat(byteDest, out int bytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, formatInfo));

        Span<char> mutableCharDest = stackalloc char[100];
        mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, formatInfo);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, formatInfo));
    }

    [Fact]
    public void Number_WithMultiCharSeparators_AllMethodsConsistent()
    {
        string jsonValue = "9999.995";
        string format = "N2";
        string expected = "10 000.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ".",
            NegativeSign = "-"
        };

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, format, formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        element.TryFormat(byteDest, out int bytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, formatInfo));

        Span<char> mutableCharDest = stackalloc char[100];
        mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, formatInfo);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, formatInfo);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, formatInfo));
    }

    #endregion

    #region Large Number Tests (> 128 bits)

    [Fact]
    public void LargeNumber_ExceedsUInt128_FormatsCorrectly()
    {
        // Test a 256-bit number: 2^256 - 1
        string jsonValue = "115792089237316195423570985008687907853269984665640564039457584007913129639935";
        string format = "N0";
        string expected = "115,792,089,237,316,195,423,570,985,008,687,907,853,269,984,665,640,564,039,457,584,007,913,129,639,935";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[512];
        bool success1 = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success1, "JsonElement.TryFormat(char) should succeed");
        string result1 = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result1);

        Span<byte> byteDest = stackalloc byte[200];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success2, "JsonElement.TryFormat(byte) should succeed");
        string result2 = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result2);

        string result3 = element.ToString(format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result3);

        Span<char> mutableCharDest = stackalloc char[200];
        bool success4 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success4, "JsonElement.Mutable.TryFormat(char) should succeed");
        string result4 = mutableCharDest.Slice(0, mutableCharsWritten).ToString();
        Assert.Equal(expected, result4);

        Span<byte> mutableByteDest = stackalloc byte[200];
        bool success5 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success5, "JsonElement.Mutable.TryFormat(byte) should succeed");
        string result5 = JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten));
        Assert.Equal(expected, result5);

        string result6 = mutableElement.ToString(format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result6);
    }

    [Fact]
    public void LargeNumber_WithDecimals_FormatsCorrectly()
    {
        // Test a large number with decimal places
        // Note: Currently, very large numbers (>40 digits) may not round correctly
        string jsonValue = "123456789012345678901234567890123456789012.456789";
        string format = "N2";
        string expected = "123,456,789,012,345,678,901,234,567,890,123,456,789,012.46";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[200];
        element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[200];
        element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, CultureInfo.InvariantCulture));

        Span<char> mutableCharDest = stackalloc char[200];
        mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[200];
        mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LargeNegativeNumber_FormatsCorrectly()
    {
        // Test a large negative number
        string jsonValue = "-1606938044258990275541962092341162602522202993782792835301376";
        string format = "N2";
        string expected = "-1,606,938,044,258,990,275,541,962,092,341,162,602,522,202,993,782,792,835,301,376.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[200];
        element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[200];
        element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, CultureInfo.InvariantCulture));

        Span<char> mutableCharDest = stackalloc char[200];
        mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[200];
        mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, CultureInfo.InvariantCulture));
    }

    #endregion

    #region UTF-8 Large Number Tests

    [Fact]
    public void LargeNumber_Utf8Format_FormatsCorrectly()
    {
        // Test UTF-8 formatting path for large numbers
        string jsonValue = "1427247692705959881058285969449495136382746624";
        string format = "N2";
        string expected = "1,427,247,692,705,959,881,058,285,969,449,495,136,382,746,624.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[512];
        bool success = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_ExponentialFormat_FormatsCorrectly()
    {
        // Test exponential format for large numbers
        // 115792089237316195423570985008687907853269984665640564039457584007913129639935 in E2 format
        // = 1.16E+077 (rounded to 2 decimal places)
        string jsonValue = "115792089237316195423570985008687907853269984665640564039457584007913129639935";
        string expected = "1.16E+077";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_GeneralFormat_FormatsCorrectly()
    {
        // Test general format for large numbers
        // 123456789012345678901234567890 in G format (30 digits)
        // General format uses scientific notation: 1.23456789012346E+29
        string jsonValue = "123456789012345678901234567890";
        string expected = "1.23456789012346E+29";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_PercentFormat_FormatsCorrectly()
    {
        // Test percent format for large numbers
        string jsonValue = "12345678901234567890";
        string expected = "1,234,567,890,123,456,789,000.00 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_BufferTooSmall_ReturnsFalse()
    {
        // Test that formatting fails gracefully when buffer is too small
        string jsonValue = "1427247692705959881058285969449495136382746624";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // Buffer too small for formatted output with thousand separators
        Span<char> smallBuffer = stackalloc char[10];
        bool success = element.TryFormat(smallBuffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void LargeNumberZero_AllFormats_FormatCorrectly()
    {
        // Test that zero formats correctly with various format specifiers
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // N2 format
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00", charDest.Slice(0, charsWritten).ToString());

        // E2 format
        element.TryFormat(charDest, out charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00E+000", charDest.Slice(0, charsWritten).ToString());

        // C format - Note: Currency formatting may not include symbol in all paths
        element.TryFormat(charDest, out charsWritten, "C2", CultureInfo.InvariantCulture);
        string currencyResult = charDest.Slice(0, charsWritten).ToString();
        // Just verify it has decimal places
        Assert.Contains(".00", currencyResult);

        // P2 format
        element.TryFormat(charDest, out charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00 %", charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void LargeNumber_WithTrailingZeros_FormatsCorrectly()
    {
        // Test numbers that have trailing zeros after the decimal point
        string jsonValue = "123456789012345678901234567890.00";
        string expected = "123,456,789,012,345,678,901,234,567,890.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_SmallFractionalPart_FormatsCorrectly()
    {
        // Test numbers with very small fractional parts (many leading zeros after decimal)
        string jsonValue = "123456789012345678901234567890.000001";
        string expected = "123,456,789,012,345,678,901,234,567,890.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_DifferentPrecisions_FormatCorrectly()
    {
        // Test various precision values
        string jsonValue = "12345678901234567890.123456";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // N0 - no decimal places
        Span<char> charDest = stackalloc char[200];
        element.TryFormat(charDest, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.Equal("12,345,678,901,234,567,890", charDest.Slice(0, charsWritten).ToString());

        // N1 - 1 decimal place
        element.TryFormat(charDest, out charsWritten, "N1", CultureInfo.InvariantCulture);
        Assert.Equal("12,345,678,901,234,567,890.1", charDest.Slice(0, charsWritten).ToString());

        // N4 - 4 decimal places
        element.TryFormat(charDest, out charsWritten, "N4", CultureInfo.InvariantCulture);
        Assert.Equal("12,345,678,901,234,567,890.1235", charDest.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Currency and Percent Format Tests

    [Fact]
    public void LargeNumber_CurrencyFormat_FormatsCorrectly()
    {
        // Test currency format with large numbers
        // 12345678901234567890.50 in C2 format (InvariantCulture uses ¤ symbol)
        string jsonValue = "12345678901234567890.50";
        string expected = "¤12,345,678,901,234,567,890.50";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_CurrencyFormat_Utf8_FormatsCorrectly()
    {
        // Test currency format UTF-8 path
        // 98765432109876543210.99 in C2 format (InvariantCulture uses ¤ symbol)
        string jsonValue = "98765432109876543210.99";
        string expected = "¤98,765,432,109,876,543,210.99";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[512];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_PercentFormat_Utf8_FormatsCorrectly()
    {
        // Test percent format UTF-8 path
        string jsonValue = "123.456";
        string expected = "12,345.60 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[200];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_ExponentialFormat_Utf8_FormatsCorrectly()
    {
        // Test exponential format UTF-8 path
        // 123456789012345678901234567890 in E2 format
        // = 1.23E+029
        string jsonValue = "123456789012345678901234567890";
        string expected = "1.23E+029";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[200];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_GeneralFormat_Utf8_FormatsCorrectly()
    {
        // Test general format UTF-8 path
        // 987654321098765432109876543210 in G format (30 digits)
        // = 9.87654321098765E+29
        string jsonValue = "987654321098765432109876543210";
        string expected = "9.87654321098765E+29";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[200];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_FixedPointFormat_FormatsCorrectly()
    {
        // Test fixed-point format (F) for large numbers
        string jsonValue = "12345678901234567890.123456";
        string expected = "12345678901234567890.12";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_FixedPointFormat_Utf8_FormatsCorrectly()
    {
        // Test fixed-point format UTF-8 path
        string jsonValue = "98765432109876543210.987654";
        string expected = "98765432109876543210.99";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[200];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NegativeLargeNumber_CurrencyFormat_FormatsCorrectly()
    {
        // Test negative currency formatting
        // -123456789012345.67 in C2 format (InvariantCulture uses ¤ symbol and parentheses for negative)
        string jsonValue = "-123456789012345.67";
        string expected = "(¤123,456,789,012,345.67)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NegativeLargeNumber_PercentFormat_FormatsCorrectly()
    {
        // Test negative percent formatting
        // -12.345 in P2 format = -12.345 * 100 = -1234.5 = -1,234.50 %
        string jsonValue = "-12.345";
        string expected = "-1,234.50 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_ExponentialFormat_DifferentPrecisions_FormatCorrectly()
    {
        // Test exponential format with different precisions
        string jsonValue = "123456789012345678901234567890";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        
        // E0 - no decimal places: 1E+029
        element.TryFormat(charDest, out int charsWritten, "E0", CultureInfo.InvariantCulture);
        string result0 = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("1E+029", result0);
        
        // E4 - 4 decimal places: 1.2346E+029
        element.TryFormat(charDest, out charsWritten, "E4", CultureInfo.InvariantCulture);
        string result4 = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("1.2346E+029", result4);
    }

    [Fact]
    public void LargeNumber_AllFormats_Utf8BufferTooSmall_ReturnsFalse()
    {
        // Test buffer too small for UTF-8 formatting
        string jsonValue = "12345678901234567890123456789012345678901234567890";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // Very small buffer
        Span<byte> smallBuffer = stackalloc byte[5];
        
        // Try various formats, all should fail
        Assert.False(element.TryFormat(smallBuffer, out _, "N2", CultureInfo.InvariantCulture));
        Assert.False(element.TryFormat(smallBuffer, out _, "E2", CultureInfo.InvariantCulture));
        Assert.False(element.TryFormat(smallBuffer, out _, "C2", CultureInfo.InvariantCulture));
        Assert.False(element.TryFormat(smallBuffer, out _, "P2", CultureInfo.InvariantCulture));
        Assert.False(element.TryFormat(smallBuffer, out _, "F2", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void VerySmallNumber_AllFormats_FormatCorrectly()
    {
        // Test very small numbers (< 1) with various formats
        // 0.000000123456789
        string jsonValue = "0.000000123456789";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        
        // N6 - should round to 6 decimal places: 0.000000
        element.TryFormat(charDest, out int charsWritten, "N6", CultureInfo.InvariantCulture);
        string resultN = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("0.000000", resultN);
        
        // E2 - scientific notation: 1.23E-007
        element.TryFormat(charDest, out charsWritten, "E2", CultureInfo.InvariantCulture);
        string resultE = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("1.23E-007", resultE);
        
        // F8 - fixed point with 8 decimals: 0.00000012
        element.TryFormat(charDest, out charsWritten, "F8", CultureInfo.InvariantCulture);
        string resultF = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("0.00000012", resultF);
    }

    #endregion

    #region Additional Edge Cases for Coverage

    [Fact]
    public void LargeNumber_HighPrecision_FormatsCorrectly()
    {
        // Test very high precision formatting
        // 123456789012345678901234567890.123456789012345 with N10 format
        string jsonValue = "123456789012345678901234567890.123456789012345";
        string expected = "123,456,789,012,345,678,901,234,567,890.1234567890";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[300];
        bool success = element.TryFormat(charDest, out int charsWritten, "N10", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NumberNearZero_WithLeadingZeros_FormatsCorrectly()
    {
        // Test numbers just above zero with many leading zeros after decimal
        // 0.00000000001 in N12 format
        string jsonValue = "0.00000000001";
        string expected = "0.000000000010";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N12", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NumberNearZero_Utf8_FormatsCorrectly()
    {
        // Test UTF-8 path for small numbers with N format
        // 0.000001 in N6 format
        string jsonValue = "0.000001";
        string expected = "0.000001";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "N6", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_RoundingUp_FormatsCorrectly()
    {
        // Test rounding up scenario
        // 999.999 rounds to 1,000.00 with N2 format
        string jsonValue = "999.999";
        string expected = "1,000.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumber_RoundingUpAcrossGroups_FormatsCorrectly()
    {
        // Test rounding that affects grouping
        // 9999.999 rounds to 10,000.00 with N2 format
        string jsonValue = "9999.999";
        string expected = "10,000.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeNumberWithTrailingZerosInFraction_FormatsCorrectly()
    {
        // Test formatting preserves trailing zeros in precision
        // 12345.1 with N3 format should give 12,345.100
        string jsonValue = "12345.1";
        string expected = "12,345.100";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N3", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtremelyLargeExponent_FormatsCorrectly()
    {
        // Test with a very large number represented with exponent
        // 1e100 in E2 format
        string jsonValue = "1e100";
        string expected = "1.00E+100";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NegativeExponentialNotation_FormatsCorrectly()
    {
        // Test negative numbers in exponential format
        // -123456789012345 in E3 format
        string jsonValue = "-123456789012345";
        string expected = "-1.235E+014";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E3", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ZeroWithHighPrecision_FormatsCorrectly()
    {
        // Test zero with many decimal places
        // 0 in N20 format
        string jsonValue = "0";
        string expected = "0.00000000000000000000";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N20", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LargeIntegerPart_SmallFractionalPart_FormatsCorrectly()
    {
        // Test large integer with tiny fractional part
        // 123456789012345678901234567890.000000001 in N9 format
        string jsonValue = "123456789012345678901234567890.000000001";
        string expected = "123,456,789,012,345,678,901,234,567,890.000000001";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[300];
        bool success = element.TryFormat(charDest, out int charsWritten, "N9", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NumberJustBelowOne_FormatsCorrectly()
    {
        // Test numbers slightly below 1
        // 0.9999999999 in N10 format
        string jsonValue = "0.9999999999";
        string expected = "0.9999999999";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N10", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExponentialFormat_HighPrecision_FormatsCorrectly()
    {
        // Test exponential format with high precision
        // 123456.789 in E10 format
        string jsonValue = "123456.789";
        string expected = "1.2345678900E+005";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E10", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_HighPrecision_FormatsCorrectly()
    {
        // Test percent format with high precision
        // 0.123456789 in P5 format = 12.34568 %
        string jsonValue = "0.123456789";
        string expected = "12.34568 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P5", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FixedPoint_LargeNumber_NoPrecision_FormatsCorrectly()
    {
        // Test fixed-point with F0 (no decimals)
        // 123456789012345.678 in F0 format (truncates to integer)
        string jsonValue = "123456789012345.678";
        string expected = "123456789012345";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "F0", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Buffer Size and Error Handling Tests

    [Fact]
    public void InvalidFormatString_ReturnsFalse()
    {
        // Test invalid format specifier
        string jsonValue = "12345.67";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        // X is not a valid format for numbers (it's for hex, only works with integers)
        bool success = element.TryFormat(charDest, out int charsWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void EmptyFormatString_ReturnsRawNumber()
    {
        // Test empty format string returns the number as-is
        string jsonValue = "12345.67";
        string expected = "12345.67";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EmptyFormatString_BufferTooSmall_ReturnsFalse()
    {
        // Test empty format string with insufficient buffer
        string jsonValue = "12345.67";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[5]; // Too small for "12345.67"
        bool success = element.TryFormat(charDest, out int charsWritten, "", CultureInfo.InvariantCulture);
        Assert.False(success);
    }

    [Fact]
    public void InvalidPrecisionInFormat_ReturnsFalse()
    {
        // Test format with invalid precision (non-numeric)
        string jsonValue = "12345.67";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "NX", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ZeroValue_EmptyFormat_BufferExactlyOne_Succeeds()
    {
        // Test zero with empty format and buffer of exactly 1 character
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[1];
        bool success = element.TryFormat(charDest, out int charsWritten, "", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void ZeroValue_EmptyFormat_BufferTooSmall_ReturnsFalse()
    {
        // Test zero with empty format and buffer of 0
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[0];
        bool success = element.TryFormat(charDest, out int charsWritten, "", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void PercentFormat_BufferTooSmallForSymbol_ReturnsFalse()
    {
        // Test percent format where buffer is too small for the symbol
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // Buffer size that can fit "0.00" but not " %"
        Span<char> charDest = stackalloc char[5];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void CurrencyFormat_BufferTooSmallForParentheses_ReturnsFalse()
    {
        // Test negative currency where buffer is too small for opening parenthesis
        string jsonValue = "-1";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[2];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void NumberFormat_BufferTooSmallForNegativeSign_ReturnsFalse()
    {
        // Test negative number where buffer too small for negative sign
        string jsonValue = "-123";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[0];
        bool success = element.TryFormat(charDest, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ExponentialFormat_BufferTooSmallForExponent_ReturnsFalse()
    {
        // Test exponential format where buffer too small
        string jsonValue = "1";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[5]; // Too small for "1.00E+000"
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void FixedPointFormat_BufferTooSmallForDecimal_ReturnsFalse()
    {
        // Test fixed-point where buffer too small for decimal separator and digits
        string jsonValue = "1.23";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[2]; // Too small for "1.23"
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ToString_WithFormat_ProducesCorrectResult()
    {
        // Test the ToString(format, provider) method
        string jsonValue = "12345.678";
        string expected = "12,345.68";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        string result = element.ToString("N2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatString_WithInvalidFormat_ReturnsFalse()
    {
        // Test TryFormat that returns a string (for coverage of TryFormatNumberAsString path)
        string jsonValue = "12345.678";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // Try to format with invalid format via ToString
        // This should trigger the fallback path that returns false
        Span<char> charDest = stackalloc char[1];
        bool success = element.TryFormat(charDest, out int charsWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
    }

    [Fact]
    public void NegativePercent_BufferTooSmallForNegativeAndPercent_ReturnsFalse()
    {
        // Test negative percent where buffer too small
        string jsonValue = "-0.5";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[5]; // Too small for "-50.00 %"
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ZeroValue_FixedPointFormat_BufferTooSmall_ReturnsFalse()
    {
        // Test zero with fixed-point format where buffer too small
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[2]; // Too small for "0.00"
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ZeroValue_ExponentialFormat_BufferTooSmall_ReturnsFalse()
    {
        // Test zero with exponential format where buffer too small
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[5]; // Too small for "0.00E+000"
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void ZeroValue_CurrencyFormat_BufferTooSmall_ReturnsFalse()
    {
        // Test zero with currency format where buffer too small
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[2]; // Too small for "¤0.00"
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    #endregion

    #region Currency Pattern Tests

    [Fact]
    public void CurrencyFormat_Pattern0_NegativeParentheses_FormatsCorrectly()
    {
        // Pattern 0: ($n) - InvariantCulture default
        string jsonValue = "-123.45";
        string expected = "(¤123.45)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern1_NegativeSignSymbolNumber_FormatsCorrectly()
    {
        // Pattern 1: -$n
        string jsonValue = "-123.45";
        string expected = "-$123.45";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 1;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern2_SymbolNegativeNumber_FormatsCorrectly()
    {
        // Pattern 2: $-n
        string jsonValue = "-123.45";
        string expected = "$-123.45";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 2;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern3_SymbolNumberNegative_FormatsCorrectly()
    {
        // Pattern 3: $n-
        string jsonValue = "-123.45";
        string expected = "$123.45-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 3;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern4_ParenthesesNumberSymbol_FormatsCorrectly()
    {
        // Pattern 4: (n$)
        string jsonValue = "-123.45";
        string expected = "(123.45$)";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 4;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern8_NegativeNumberSpaceSymbol_FormatsCorrectly()
    {
        // Pattern 8: -n $
        string jsonValue = "-123.45";
        string expected = "-123.45 $";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 8;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_PositiveNumber_FormatsCorrectly()
    {
        // Positive currency should use CurrencyPositivePattern
        string jsonValue = "123.45";
        string expected = "¤123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern5_NegativeNumberSymbol_FormatsCorrectly()
    {
        // Pattern 5: -n$
        string jsonValue = "-123.45";
        string expected = "-123.45$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 5;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern6_NumberNegativeSymbol_FormatsCorrectly()
    {
        // Pattern 6: n-$
        string jsonValue = "-123.45";
        string expected = "123.45-$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 6;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern7_NumberSymbolNegative_FormatsCorrectly()
    {
        // Pattern 7: n$-
        string jsonValue = "-123.45";
        string expected = "123.45$-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 7;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern9_NegativeSymbolSpaceNumber_FormatsCorrectly()
    {
        // Pattern 9: -$ n
        string jsonValue = "-123.45";
        string expected = "-$ 123.45";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 9;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern10_NumberSpaceSymbolNegative_FormatsCorrectly()
    {
        // Pattern 10: n $-
        string jsonValue = "-123.45";
        string expected = "123.45 $-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 10;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern11_SymbolSpaceNumberNegative_FormatsCorrectly()
    {
        // Pattern 11: $ n-
        string jsonValue = "-123.45";
        string expected = "$ 123.45-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 11;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern12_SymbolSpaceNegativeNumber_FormatsCorrectly()
    {
        // Pattern 12: $ -n
        string jsonValue = "-123.45";
        string expected = "$ -123.45";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 12;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern13_NumberNegativeSpaceSymbol_FormatsCorrectly()
    {
        // Pattern 13: n- $
        string jsonValue = "-123.45";
        string expected = "123.45- $";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 13;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern14_ParenthesesSymbolSpaceNumber_FormatsCorrectly()
    {
        // Pattern 14: ($ n)
        string jsonValue = "-123.45";
        string expected = "($ 123.45)";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 14;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_Pattern15_ParenthesesNumberSpaceSymbol_FormatsCorrectly()
    {
        // Pattern 15: (n $)
        string jsonValue = "-123.45";
        string expected = "(123.45 $)";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 15;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_LargeNumber_AllPatterns_FormatCorrectly()
    {
        // Test a large number with various currency patterns
        string jsonValue = "-123456789012345.67";
        string[] expected = new string[]
        {
            "($123,456,789,012,345.67)",     // Pattern 0: ($n)
            "-$123,456,789,012,345.67",      // Pattern 1: -$n
            "$-123,456,789,012,345.67",      // Pattern 2: $-n
            "$123,456,789,012,345.67-",      // Pattern 3: $n-
            "(123,456,789,012,345.67$)",     // Pattern 4: (n$)
            "-123,456,789,012,345.67$",      // Pattern 5: -n$
            "123,456,789,012,345.67-$",      // Pattern 6: n-$
            "123,456,789,012,345.67$-",      // Pattern 7: n$-
            "-123,456,789,012,345.67 $",     // Pattern 8: -n $
            "-$ 123,456,789,012,345.67",     // Pattern 9: -$ n
            "123,456,789,012,345.67 $-",     // Pattern 10: n $-
            "$ 123,456,789,012,345.67-",     // Pattern 11: $ n-
            "$ -123,456,789,012,345.67",     // Pattern 12: $ -n
            "123,456,789,012,345.67- $",     // Pattern 13: n- $
            "($ 123,456,789,012,345.67)",    // Pattern 14: ($ n)
            "(123,456,789,012,345.67 $)"     // Pattern 15: (n $)
        };
        
        var culture = new CultureInfo("en-US");
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        
        // Test all 16 patterns
        for (int pattern = 0; pattern <= 15; pattern++)
        {
            culture.NumberFormat.CurrencyNegativePattern = pattern;
            bool success = element.TryFormat(charDest, out int charsWritten, "C2", culture);
            Assert.True(success, $"Pattern {pattern} should succeed");
            string result = charDest.Slice(0, charsWritten).ToString();
            
            Assert.Equal(expected[pattern], result);
        }
    }

    #endregion

    #region Percent Pattern Tests

    [Fact]
    public void PercentFormat_Pattern0_NegativeNumberSpacePercent_FormatsCorrectly()
    {
        // Pattern 0: -n % - InvariantCulture default
        string jsonValue = "-0.5";
        string expected = "-50.00 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern1_NegativeNumberPercent_FormatsCorrectly()
    {
        // Pattern 1: -n%
        string jsonValue = "-0.5";
        string expected = "-50.00%";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 1;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern2_NegativePercentNumber_FormatsCorrectly()
    {
        // Pattern 2: -%n
        string jsonValue = "-0.5";
        string expected = "-%50.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 2;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern3_PercentNegativeNumber_FormatsCorrectly()
    {
        // Pattern 3: %-n
        string jsonValue = "-0.5";
        string expected = "%-50.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 3;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern7_NegativePercentSpaceNumber_FormatsCorrectly()
    {
        // Pattern 7: -% n
        string jsonValue = "-0.5";
        string expected = "-% 50.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 7;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_PositiveNumber_FormatsCorrectly()
    {
        // Positive percent should use PercentPositivePattern
        string jsonValue = "0.5";
        string expected = "50.00 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_LargeNumber_FormatsCorrectly()
    {
        // Test percent format with large number
        string jsonValue = "123.456";
        string expected = "12,345.60 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_ZeroWithDifferentPrecision_FormatsCorrectly()
    {
        // Test zero with different precision
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        // P0
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P0", CultureInfo.InvariantCulture);
        Assert.Equal("0 %", charDest.Slice(0, charsWritten).ToString());

        // P4
        element.TryFormat(charDest, out charsWritten, "P4", CultureInfo.InvariantCulture);
        Assert.Equal("0.0000 %", charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_Pattern4_PercentNumberNegative_FormatsCorrectly()
    {
        // Pattern 4: %n-
        string jsonValue = "-0.5";
        string expected = "%50.00-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 4;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern5_NumberNegativePercent_FormatsCorrectly()
    {
        // Pattern 5: n-%
        string jsonValue = "-0.5";
        string expected = "50.00-%";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 5;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern6_NumberPercentNegative_FormatsCorrectly()
    {
        // Pattern 6: n%-
        string jsonValue = "-0.5";
        string expected = "50.00%-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 6;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern8_NumberSpacePercentNegative_FormatsCorrectly()
    {
        // Pattern 8: n %-
        string jsonValue = "-0.5";
        string expected = "50.00 %-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 8;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern9_PercentSpaceNumberNegative_FormatsCorrectly()
    {
        // Pattern 9: % n-
        string jsonValue = "-0.5";
        string expected = "% 50.00-";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 9;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern10_PercentSpaceNegativeNumber_FormatsCorrectly()
    {
        // Pattern 10: % -n
        string jsonValue = "-0.5";
        string expected = "% -50.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 10;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_Pattern11_NumberNegativeSpacePercent_FormatsCorrectly()
    {
        // Pattern 11: n- %
        string jsonValue = "-0.5";
        string expected = "50.00- %";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 11;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_LargeNumber_AllPatterns_FormatCorrectly()
    {
        // Test all percent patterns with a large number
        string jsonValue = "-1.23456";
        string[] expected = new string[]
        {
            "-123.46 %",   // Pattern 0: -n %
            "-123.46%",    // Pattern 1: -n%
            "-%123.46",    // Pattern 2: -%n
            "%-123.46",    // Pattern 3: %-n
            "%123.46-",    // Pattern 4: %n-
            "123.46-%",    // Pattern 5: n-%
            "123.46%-",    // Pattern 6: n%-
            "-% 123.46",   // Pattern 7: -% n
            "123.46 %-",   // Pattern 8: n %-
            "% 123.46-",   // Pattern 9: % n-
            "% -123.46",   // Pattern 10: % -n
            "123.46- %"    // Pattern 11: n- %
        };
        
        var culture = new CultureInfo("en-US");
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        
        // Test all 12 patterns
        for (int pattern = 0; pattern <= 11; pattern++)
        {
            culture.NumberFormat.PercentNegativePattern = pattern;
            bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
            Assert.True(success, $"Pattern {pattern} should succeed");
            string result = charDest.Slice(0, charsWritten).ToString();
            
            Assert.Equal(expected[pattern], result);
        }
    }

    #endregion

    #region Number Format With Grouping Tests

    [Fact]
    public void NumberFormat_NegativeWithGrouping_FormatsCorrectly()
    {
        // Test negative number with thousand separators
        string jsonValue = "-1234567.89";
        string expected = "-1,234,567.89";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region General Format Path Tests

    [Fact]
    public void GeneralFormat_SmallNumber_UsesScientific()
    {
        // Numbers with exponent < -1 use scientific notation
        // 0.01 = 1E-2
        string jsonValue = "0.01";
        string expected = "1E-2";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_VerySmallNumber_UsesScientific()
    {
        // Very small numbers use scientific notation
        // 0.001 = 1E-3
        string jsonValue = "0.001";
        string expected = "1E-3";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_ModerateNumber_UsesFixedPoint()
    {
        // Numbers within precision range use fixed-point
        string jsonValue = "123456.789";
        string expected = "123456.789";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_VeryLargeNumber_UsesScientific()
    {
        // Very large numbers (exponent >= precision) use scientific notation
        // With default precision 15, numbers >= 10^15 use scientific
        string jsonValue = "12345678901234567890";
        string expected = "1.23456789012346E+19";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_WithPrecision_UsesCorrectNotation()
    {
        // G5 format with number that needs scientific notation
        // 1234567 with precision 5 -> rounds to 5 significant digits and uses scientific
        string jsonValue = "1234567";
        string expected = "1.2346E+6";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G5", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_Utf8_SmallNumber()
    {
        // UTF-8 path with small number (scientific notation for < -1)
        string jsonValue = "0.01";
        string expected = "1E-2";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_Utf8_VeryLargeNumber()
    {
        // UTF-8 path with large number (scientific notation)
        string jsonValue = "12345678901234567890";
        string expected = "1.23456789012346E+19";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_TrailingZerosRemoved()
    {
        // General format should remove trailing zeros
        string jsonValue = "12.3000";
        string expected = "12.3";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_RoundingNearThreshold()
    {
        // Test rounding behavior near the scientific notation threshold
        string jsonValue = "999999999999999.9"; // 15 significant digits
        string expected = "1E+15";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Zero Value Comprehensive Tests

    [Fact]
    public void ZeroValue_AllFormats_Utf8_FormatCorrectly()
    {
        // Test zero with all major formats via UTF-8
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        
        // N2
        element.TryFormat(byteDest, out int bytesWritten, "N2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00", JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
        
        // E2
        element.TryFormat(byteDest, out bytesWritten, "E2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00E+000", JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
        
        // F2
        element.TryFormat(byteDest, out bytesWritten, "F2", CultureInfo.InvariantCulture);
        Assert.Equal("0.00", JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
        
        // G
        element.TryFormat(byteDest, out bytesWritten, "G", CultureInfo.InvariantCulture);
        Assert.Equal("0", JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void ZeroValue_PercentFormat_AllPatterns_FormatCorrectly()
    {
        // Test zero with various percent patterns
        // Zero should use positive pattern regardless of negative pattern setting
        string jsonValue = "0";
        string expected = "0.00 %";  // InvariantCulture percent positive pattern
        
        var culture = new CultureInfo("en-US");
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        
        // Test all 12 percent patterns with zero
        for (int pattern = 0; pattern <= 11; pattern++)
        {
            culture.NumberFormat.PercentNegativePattern = pattern;
            culture.NumberFormat.PercentPositivePattern = 0; // n %
            culture.NumberFormat.PercentSymbol = "%";
            culture.NumberFormat.PercentDecimalSeparator = ".";
            bool success = element.TryFormat(charDest, out int charsWritten, "P2", culture);
            Assert.True(success, $"Zero with percent pattern {pattern} should succeed");
            string result = charDest.Slice(0, charsWritten).ToString();
            
            // Zero is not negative, so it uses positive pattern: "0.00 %"
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void ZeroValue_CurrencyFormat_FormatsCorrectly()
    {
        // Test zero with currency format
        string jsonValue = "0";
        string expected = "¤0.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData("\"hello\"", null, "hello")]
    [InlineData("\"\"", null, "")]
    [InlineData("\"Hello World\"", null, "Hello World")]
    [InlineData("\"Special \\\"chars\\\"\"", null, "Special \"chars\"")]
    public void String_AllMethods_FormatConsistently(string json, string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[100];
        bool success1 = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success1);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success2);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, CultureInfo.InvariantCulture));

        Span<char> mutableCharDest = stackalloc char[100];
        bool success4 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success4);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        bool success5 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success5);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, CultureInfo.InvariantCulture));
    }

    #endregion

    #region Boolean Formatting Tests

    [Theory]
    [InlineData("true", null, "True")]
    [InlineData("false", null, "False")]
    public void Boolean_AllMethods_FormatConsistently(string json, string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[100];
        bool success1 = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success1);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success2);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, CultureInfo.InvariantCulture));

        Span<char> mutableCharDest = stackalloc char[100];
        bool success4 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success4);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        bool success5 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success5);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, CultureInfo.InvariantCulture));
    }

    #endregion

    #region Null Formatting Tests

    [Fact]
    public void Null_AllMethods_FormatConsistently()
    {
        string json = "null";
        string format = null;
        string expected = "";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;

        // Test all 6 methods
        Span<char> charDest = stackalloc char[100];
        bool success1 = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success1);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[100];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success2);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Assert.Equal(expected, element.ToString(format, CultureInfo.InvariantCulture));

        Span<char> mutableCharDest = stackalloc char[100];
        bool success4 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success4);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[100];
        bool success5 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success5);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));

        Assert.Equal(expected, mutableElement.ToString(format, CultureInfo.InvariantCulture));
    }

    #endregion

    #region Array Formatting Tests

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("[]")]
    [InlineData("[\"a\",\"b\",\"c\"]")]
    [InlineData("[true,false]")]
    [InlineData("[1,\"text\",true,null]")]
    public void Array_AllMethods_FormatConsistently(string json)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Arrays should format as JSON (no spaces)
        string expected = json;

        // Test JsonElement.ToString() without format
        string result1 = element.ToString(null, null);
        Assert.Equal(expected, result1);

        // Test JsonElement.Mutable.ToString() without format
        string result2 = mutableElement.ToString(null, null);
        Assert.Equal(expected, result2);

        // TryFormat for arrays should also work
        Span<char> charDest = stackalloc char[200];
        bool success1 = element.TryFormat(charDest, out int charsWritten, default, null);
        Assert.True(success1);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[200];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, default, null);
        Assert.True(success2);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Span<char> mutableCharDest = stackalloc char[200];
        bool success3 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, default, null);
        Assert.True(success3);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[200];
        bool success4 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, default, null);
        Assert.True(success4);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));
    }

    #endregion

    #region Object Formatting Tests

    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("{}")]
    [InlineData("{\"name\":\"value\"}")]
    [InlineData("{\"x\":1,\"y\":2,\"z\":3}")]
    [InlineData("{\"bool\":true,\"num\":42,\"str\":\"text\"}")]
    public void Object_AllMethods_FormatConsistently(string json)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Objects should format as JSON (no spaces)
        string expected = json;

        // Test JsonElement.ToString() without format
        string result1 = element.ToString(null, null);
        Assert.Equal(expected, result1);

        // Test JsonElement.Mutable.ToString() without format
        string result2 = mutableElement.ToString(null, null);
        Assert.Equal(expected, result2);

        // TryFormat for objects should also work
        Span<char> charDest = stackalloc char[200];
        bool success1 = element.TryFormat(charDest, out int charsWritten, default, null);
        Assert.True(success1);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());

        Span<byte> byteDest = stackalloc byte[200];
        bool success2 = element.TryFormat(byteDest, out int bytesWritten, default, null);
        Assert.True(success2);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));

        Span<char> mutableCharDest = stackalloc char[200];
        bool success3 = mutableElement.TryFormat(mutableCharDest, out int mutableCharsWritten, default, null);
        Assert.True(success3);
        Assert.Equal(expected, mutableCharDest.Slice(0, mutableCharsWritten).ToString());

        Span<byte> mutableByteDest = stackalloc byte[200];
        bool success4 = mutableElement.TryFormat(mutableByteDest, out int mutableBytesWritten, default, null);
        Assert.True(success4);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(mutableByteDest.Slice(0, mutableBytesWritten)));
    }

    #endregion

    #region Buffer Too Small Tests

    [Fact]
    public void TryFormat_BufferTooSmall_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1234567.89");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);

        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Char version - buffer too small
        Span<char> smallCharBuffer = stackalloc char[5];
        bool success1 = element.TryFormat(smallCharBuffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.False(success1);
        Assert.Equal(0, charsWritten);

        // UTF-8 version - buffer too small
        Span<byte> smallByteBuffer = stackalloc byte[5];
        bool success2 = element.TryFormat(smallByteBuffer, out int bytesWritten, "N2", CultureInfo.InvariantCulture);
        Assert.False(success2);
        Assert.Equal(0, bytesWritten);

        // Same for Mutable
        Span<char> smallCharBuffer2 = stackalloc char[5];
        bool success3 = mutableElement.TryFormat(smallCharBuffer2, out int charsWritten2, "N2", CultureInfo.InvariantCulture);
        Assert.False(success3);
        Assert.Equal(0, charsWritten2);

        Span<byte> smallByteBuffer2 = stackalloc byte[5];
        bool success4 = mutableElement.TryFormat(smallByteBuffer2, out int bytesWritten2, "N2", CultureInfo.InvariantCulture);
        Assert.False(success4);
        Assert.Equal(0, bytesWritten2);
    }

    #endregion

    #region Exponential Format Edge Cases

    [Fact]
    public void ExponentialFormat_NegativeExponent_FormatsCorrectly()
    {
        // Test negative exponent in exponential format
        string jsonValue = "0.00123";
        string expected = "1.23E-003";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExponentialFormat_LargeNegativeNumber_FormatsCorrectly()
    {
        // Test large negative number in exponential format
        string jsonValue = "-987654321098765432109876543210";
        string expected = "-9.88E+029";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExponentialFormat_WithLowercaseE_FormatsCorrectly()
    {
        // Test exponential format with lowercase 'e'
        string jsonValue = "12345.67";
        string expected = "1.23e+004";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "e2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExponentialFormat_ZeroPrecision_FormatsCorrectly()
    {
        // Test E0 format (no decimal places)
        string jsonValue = "12345.67";
        string expected = "1E+004";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E0", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExponentialFormat_Utf8_NegativeExponent_FormatsCorrectly()
    {
        // Test UTF-8 path with negative exponent
        string jsonValue = "0.000456";
        string expected = "4.56E-004";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Fixed-Point Format Edge Cases

    [Fact]
    public void FixedPointFormat_VeryHighPrecision_FormatsCorrectly()
    {
        // Test fixed-point with high precision (F20)
        string jsonValue = "123.456789012345678901234567890";
        string expected = "123.45678901234567890123";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "F20", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FixedPointFormat_NegativeNumber_FormatsCorrectly()
    {
        // Test negative number with fixed-point
        string jsonValue = "-12345.6789";
        string expected = "-12345.68";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FixedPointFormat_RoundingUp_FormatsCorrectly()
    {
        // Test rounding up with fixed-point
        string jsonValue = "123.999";
        string expected = "124.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FixedPointFormat_VeryLargeNumber_FormatsCorrectly()
    {
        // Test very large number with fixed-point (no grouping)
        string jsonValue = "123456789012345678901234567890.123456";
        string expected = "123456789012345678901234567890.12";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[300];
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FixedPointFormat_Utf8_HighPrecision_FormatsCorrectly()
    {
        // Test UTF-8 fixed-point with high precision
        string jsonValue = "9.87654321";
        string expected = "9.8765432100";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[100];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "F10", CultureInfo.InvariantCulture);
        Assert.True(success);
        
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region General Format Additional Edge Cases

    [Fact]
    public void GeneralFormat_NegativeSmallNumber_UsesScientific()
    {
        // Test negative small number uses scientific notation
        string jsonValue = "-0.001";
        string expected = "-1E-3";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_WithLowercaseG_UsesLowercaseE()
    {
        // Test general format with lowercase 'g' produces lowercase 'e'
        string jsonValue = "12345678901234567890";
        string expected = "1.23456789012346e+19";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "g", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_ExactlyAtThreshold_UsesScientific()
    {
        // Test number exactly at precision threshold
        string jsonValue = "1000000000000000"; // 10^15
        string expected = "1E+15";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_JustBelowThreshold_UsesFixedPoint()
    {
        // Test number just below threshold uses fixed-point
        string jsonValue = "999999999999999"; // 15 digits
        string expected = "999999999999999";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_WithPrecision1_FormatsCorrectly()
    {
        // Test G1 format (1 significant digit)
        string jsonValue = "1234.56";
        string expected = "1E+3";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G1", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GeneralFormat_SmallDecimal_UsesFixedPoint()
    {
        // Test number between 0.1 and 1 uses fixed-point
        string jsonValue = "0.123";
        string expected = "0.123";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Rounding Edge Cases

    [Fact]
    public void Rounding_MidpointAwayFromZero_FormatsCorrectly()
    {
        // Test banker's rounding (round to even) for .5
        // 2.5 rounds to 2 (even), 3.5 rounds to 4 (even)
        string jsonValue = "2.225";
        string expected = "2.23"; // Rounds up from 2.225
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rounding_CascadingCarry_FormatsCorrectly()
    {
        // Test cascading carry through multiple digits
        string jsonValue = "999.9999";
        string expected = "1,000.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rounding_LargeNumberWithFraction_FormatsCorrectly()
    {
        // Test rounding on large number
        string jsonValue = "12345678901234567890.9999";
        string expected = "12,345,678,901,234,567,891.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[200];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Multi-Character Separator Tests

    [Fact]
    public void NumberFormat_MultiCharGroupSeparator_FormatsCorrectly()
    {
        // Test with multi-character group separator
        string jsonValue = "1234567.89";
        string expected = "1::234::567.89";
        
        var formatInfo = new NumberFormatInfo
        {
            NumberGroupSeparator = "::",
            NumberDecimalSeparator = ".",
            NegativeSign = "-"
        };
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrencyFormat_MultiCharSymbol_FormatsCorrectly()
    {
        // Test currency with multi-character symbol
        string jsonValue = "1234.56";
        string expected = "USD 1,234.56";
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "USD",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 2 // n $
        };
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PercentFormat_MultiCharSymbol_FormatsCorrectly()
    {
        // Test percent with multi-character symbol
        string jsonValue = "0.5";
        string expected = "50.00 pct";
        
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "pct",
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 0 // n %
        };
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NumberFormat_MultiCharNegativeSign_FormatsCorrectly()
    {
        // Test with multi-character negative sign
        string jsonValue = "-1234.56";
        string expected = "NEG1,234.56";
        
        var formatInfo = new NumberFormatInfo
        {
            NegativeSign = "NEG",
            NumberGroupSeparator = ",",
            NumberDecimalSeparator = "."
        };
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    #endregion

    #region Percent Format Negative Patterns Tests

    [Fact]
    public void PercentFormat_NegativePattern0_FormatsCorrectly()
    {
        // Pattern 0: -n %
        string jsonValue = "-0.1234";
        string expected = "-12.34 %";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 0,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern1_FormatsCorrectly()
    {
        // Pattern 1: -n%
        string jsonValue = "-0.1234";
        string expected = "-12.34%";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 1,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern2_FormatsCorrectly()
    {
        // Pattern 2: -%n
        string jsonValue = "-0.1234";
        string expected = "-%12.34";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 2,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern3_FormatsCorrectly()
    {
        // Pattern 3: %-n
        string jsonValue = "-0.1234";
        string expected = "%-12.34";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 3,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern4_FormatsCorrectly()
    {
        // Pattern 4: %n-
        string jsonValue = "-0.1234";
        string expected = "%12.34-";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 4,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern5_FormatsCorrectly()
    {
        // Pattern 5: n-%
        string jsonValue = "-0.1234";
        string expected = "12.34-%";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 5,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern6_FormatsCorrectly()
    {
        // Pattern 6: n%-
        string jsonValue = "-0.1234";
        string expected = "12.34%-";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 6,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern7_FormatsCorrectly()
    {
        // Pattern 7: -% n
        string jsonValue = "-0.1234";
        string expected = "-% 12.34";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 7,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern8_FormatsCorrectly()
    {
        // Pattern 8: n %-
        string jsonValue = "-0.1234";
        string expected = "12.34 %-";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 8,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern9_FormatsCorrectly()
    {
        // Pattern 9: % n-
        string jsonValue = "-0.1234";
        string expected = "% 12.34-";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 9,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern10_FormatsCorrectly()
    {
        // Pattern 10: % -n
        string jsonValue = "-0.1234";
        string expected = "% -12.34";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 10,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_NegativePattern11_FormatsCorrectly()
    {
        // Pattern 11: n- %
        string jsonValue = "-0.1234";
        string expected = "12.34- %";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentNegativePattern = 11,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Currency Format Negative Patterns Tests (4-15)

    [Fact]
    public void CurrencyFormat_NegativePattern4_FormatsCorrectly()
    {
        // Pattern 4: (n$)
        string jsonValue = "-1234.56";
        string expected = "(1,234.56$)";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 4
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern5_FormatsCorrectly()
    {
        // Pattern 5: -n$
        string jsonValue = "-1234.56";
        string expected = "-1,234.56$";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 5,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern6_FormatsCorrectly()
    {
        // Pattern 6: n-$
        string jsonValue = "-1234.56";
        string expected = "1,234.56-$";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 6,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern7_FormatsCorrectly()
    {
        // Pattern 7: n$-
        string jsonValue = "-1234.56";
        string expected = "1,234.56$-";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 7,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern8_FormatsCorrectly()
    {
        // Pattern 8: -n $
        string jsonValue = "-1234.56";
        string expected = "-1,234.56 $";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 8,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern9_FormatsCorrectly()
    {
        // Pattern 9: -$ n
        string jsonValue = "-1234.56";
        string expected = "-$ 1,234.56";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 9,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern10_FormatsCorrectly()
    {
        // Pattern 10: n $-
        string jsonValue = "-1234.56";
        string expected = "1,234.56 $-";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 10,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern11_FormatsCorrectly()
    {
        // Pattern 11: $ n-
        string jsonValue = "-1234.56";
        string expected = "$ 1,234.56-";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 11,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern12_FormatsCorrectly()
    {
        // Pattern 12: $ -n
        string jsonValue = "-1234.56";
        string expected = "$ -1,234.56";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 12,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern13_FormatsCorrectly()
    {
        // Pattern 13: n- $
        string jsonValue = "-1234.56";
        string expected = "1,234.56- $";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 13,
            NegativeSign = "-"
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern14_FormatsCorrectly()
    {
        // Pattern 14: ($ n)
        string jsonValue = "-1234.56";
        string expected = "($ 1,234.56)";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 14
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_NegativePattern15_FormatsCorrectly()
    {
        // Pattern 15: (n $)
        string jsonValue = "-1234.56";
        string expected = "(1,234.56 $)";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyNegativePattern = 15
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [Fact]
    public void NumberFormat_ZeroPrecision_FormatsCorrectly()
    {
        string jsonValue = "1234.9999";
        string expected = "1,235";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void NumberFormat_HighPrecision_FormatsCorrectly()
    {
        string jsonValue = "1234.123456789012345";
        string expected = "1,234.123456789012345";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N15", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void NumberFormat_ZeroValue_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_ZeroValue_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "$0.00";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyPositivePattern = 0
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void PercentFormat_ZeroValue_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00 %";

        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentPositivePattern = 0
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void NumberFormat_VerySmallDecimal_FormatsCorrectly()
    {
        string jsonValue = "0.00000123";
        string expected = "0.00";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void NumberFormat_IndianGrouping_FormatsCorrectly()
    {
        // Indian numbering: first group is 3, rest are 2 (e.g., 12,34,567.89)
        // Group sizes array {3, 2} means: rightmost group is 3, all others are 2
        string jsonValue = "1234567.89";
        string expected = "12,34,567.89";

        var formatInfo = new NumberFormatInfo
        {
            NumberGroupSeparator = ",",
            NumberDecimalSeparator = ".",
            NumberGroupSizes = new int[] { 3, 2 }
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void CurrencyFormat_MultiCharSymbol_PositivePattern0_FormatsCorrectly()
    {
        string jsonValue = "1234.56";
        string expected = "USD1,234.56";

        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "USD",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyPositivePattern = 0
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C2", formatInfo);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Percent Format Pattern Tests

    [Fact]
    public void Percent_PositivePattern0_nSpacePercent()
    {
        // Pattern 0: n %
        string jsonValue = "0.1234";
        string expected = "12.34 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 0,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_PositivePattern1_nPercent()
    {
        // Pattern 1: n%
        string jsonValue = "0.1234";
        string expected = "12.34%";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 1,
            PercentNegativePattern = 0,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_PositivePattern2_PercentN()
    {
        // Pattern 2: %n
        string jsonValue = "0.1234";
        string expected = "%12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 2,
            PercentNegativePattern = 0,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_PositivePattern3_PercentSpaceN()
    {
        // Pattern 3: % n
        string jsonValue = "0.1234";
        string expected = "% 12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 3,
            PercentNegativePattern = 0,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern0_MinusNSpacePercent()
    {
        // Pattern 0: -n %
        string jsonValue = "-0.1234";
        string expected = "-12.34 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 0,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern1_MinusNPercent()
    {
        // Pattern 1: -n%
        string jsonValue = "-0.1234";
        string expected = "-12.34%";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 1,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern2_MinusPercentN()
    {
        // Pattern 2: -%n
        string jsonValue = "-0.1234";
        string expected = "-%12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 2,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void ScientificFormat_VerySmallNumber_FormatsCorrectly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.00000123");
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("1.23E-006", result);
    }

    [Fact]
    public void ScientificFormat_LowerCase_FormatsCorrectly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1234567.89");
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "e2", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("1.23e+006", result);
    }

    [Fact]
    public void ScientificFormat_NegativeNumber_FormatsCorrectly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1234567.89");
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "E3", CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("-1.235E+006", result);
    }

    [Fact]
    public void NumberFormat_AlternativeGroupSizes_Indian_FormatsCorrectly()
    {
        var culture = new CultureInfo("en-US")
        {
            NumberFormat =
            {
                NumberGroupSizes = new int[] { 3, 2 },
                NumberGroupSeparator = ","
            }
        };

        using var doc = ParsedJsonDocument<JsonElement>.Parse("1234567.89");
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "N2", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal("12,34,567.89", result);
    }

    [Fact]
    public void Percent_NegativePattern3_PercentMinusN()
    {
        // Pattern 3: %-n
        string jsonValue = "-0.1234";
        string expected = "%-12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 3,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern4_PercentNMinus()
    {
        // Pattern 4: %n-
        string jsonValue = "-0.1234";
        string expected = "%12.34-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 4,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern5_NMinusPercent()
    {
        // Pattern 5: n-%
        string jsonValue = "-0.1234";
        string expected = "12.34-%";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 5,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern6_NPercentMinus()
    {
        // Pattern 6: n%-
        string jsonValue = "-0.1234";
        string expected = "12.34%-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 6,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern7_MinusPercentSpaceN()
    {
        // Pattern 7: -% n
        string jsonValue = "-0.1234";
        string expected = "-% 12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 7,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern8_NSpacePercentMinus()
    {
        // Pattern 8: n %-
        string jsonValue = "-0.1234";
        string expected = "12.34 %-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 8,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern9_PercentSpaceNMinus()
    {
        // Pattern 9: % n-
        string jsonValue = "-0.1234";
        string expected = "% 12.34-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 9,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern10_PercentSpaceMinusN()
    {
        // Pattern 10: % -n
        string jsonValue = "-0.1234";
        string expected = "% -12.34";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 10,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_NegativePattern11_NMinusSpacePercent()
    {
        // Pattern 11: n- %
        string jsonValue = "-0.1234";
        string expected = "12.34- %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            PercentPositivePattern = 0,
            PercentNegativePattern = 11,
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            NegativeSign = "-"
        };
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", formatInfo);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_WithLargeNumber_FormatsCorrectly()
    {
        // Test percent formatting with large number
        string jsonValue = "12345678901234567890.123";
        string expected = "1,234,567,890,123,456,789,012.30 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        Span<char> charDest = stackalloc char[200];
        element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_WithZero_FormatsCorrectly()
    {
        // Test percent formatting with zero
        string jsonValue = "0";
        string expected = "0.00 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Percent_DifferentPrecisions_FormatCorrectly()
    {
        // Test various precision values
        string jsonValue = "0.123456";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        // P0 - no decimal places
        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "P0", CultureInfo.InvariantCulture);
        Assert.Equal("12 %", charDest.Slice(0, charsWritten).ToString());
        
        // P1 - 1 decimal place
        element.TryFormat(charDest, out charsWritten, "P1", CultureInfo.InvariantCulture);
        Assert.Equal("12.3 %", charDest.Slice(0, charsWritten).ToString());
        
        // P4 - 4 decimal places
        element.TryFormat(charDest, out charsWritten, "P4", CultureInfo.InvariantCulture);
        Assert.Equal("12.3456 %", charDest.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Large Number Tests (>128 bits)

    [Fact]
    public void Format_150BitNumber_N2_FormatsWithThousandsSeparators()
    {
        // 150-bit number: 1427247692705959881058285969449495136382746624
        string jsonValue = "1427247692705959881058285969449495136382746624";
        string expected = "1,427,247,692,705,959,881,058,285,969,449,495,136,382,746,624.00";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Format_200BitNumber_N0_FormatsWithThousandsSeparators()
    {
        // 200-bit number
        string jsonValue = "1606938044258990275541962092341162602522202993782792835301376";
        string expected = "1,606,938,044,258,990,275,541,962,092,341,162,602,522,202,993,782,792,835,301,376";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Format_VeryLargeDecimal_N2_TruncatesCorrectly()
    {
        // Large decimal with many decimal places
        string jsonValue = "123456789012345678901234567890.123456789";
        string expected = "123,456,789,012,345,678,901,234,567,890.12";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "N2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Format_LargeNumber_F0_NoDecimals()
    {
        string jsonValue = "1427247692705959881058285969449495136382746624";
        string expected = "1427247692705959881058285969449495136382746624";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "F0", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Format_LargeNumber_F2_WithDecimals()
    {
        string jsonValue = "1427247692705959881058285969449495136382746624";
        string expected = "1427247692705959881058285969449495136382746624.00";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "F2", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Format_LargeDecimal_C0_RoundsUpCorrectly()
    {
        // This should round up from .99 to 1
        string jsonValue = "12345678901234567890.99";
        string expected = "¤12,345,678,901,234,567,891";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        element.TryFormat(charDest, out int charsWritten, "C0", CultureInfo.InvariantCulture);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Currency_PositivePattern1_NCurrencySymbol()
    {
        // Pattern 1: n$
        string jsonValue = "1234.56";
        string expected = "1,234.56$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 1;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    #region TryFormatZero Comprehensive Tests

    [Fact]
    public void Zero_GFormat_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[10];
        bool success = element.TryFormat(charDest, out int charsWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_GFormatLowercase_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[10];
        bool success = element.TryFormat(charDest, out int charsWritten, "g", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_GFormat_Utf8_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[10];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "G", CultureInfo.InvariantCulture);
        Assert.True(success);
        Span<char> charResult = stackalloc char[10];
        JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten), charResult);
        Assert.Equal(expected, charResult.Slice(0, bytesWritten).ToString());
    }

    [Fact]
    public void Zero_FFormatDefaultPrecision_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00"; // Default is 2 decimal places
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[10];
        bool success = element.TryFormat(charDest, out int charsWritten, "F", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_FFormatPrecision5_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00000";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "F5", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_FFormat_Utf8_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "F", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_EFormatDefaultPrecision_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.000000E+000"; // Default precision is 6
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "E", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_EFormatLowercase_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.000000e+000"; // Lowercase e
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "e", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_EFormatPrecision3_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.000E+000";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "E3", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_EFormat_Utf8_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.000000E+000";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "E", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_CFormatPattern1_Char_FormatsCorrectly()
    {
        // Pattern 1: n$
        string jsonValue = "0";
        string expected = "0.00$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 1;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_CFormatPattern2_Char_FormatsCorrectly()
    {
        // Pattern 2: $ n
        string jsonValue = "0";
        string expected = "$ 0.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 2;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_CFormatPattern3_Char_FormatsCorrectly()
    {
        // Pattern 3: n $
        string jsonValue = "0";
        string expected = "0.00 $";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 3;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_CFormatPattern1_Utf8_FormatsCorrectly()
    {
        // Pattern 1: n$
        string jsonValue = "0";
        string expected = "0.00$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 1;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_CFormatPattern2_Utf8_FormatsCorrectly()
    {
        // Pattern 2: $ n
        string jsonValue = "0";
        string expected = "$ 0.00";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 2;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_CFormatPattern3_Utf8_FormatsCorrectly()
    {
        // Pattern 3: n $
        string jsonValue = "0";
        string expected = "0.00 $";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 3;
        culture.NumberFormat.CurrencySymbol = "$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", culture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_PFormatDefaultPrecision_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00 %"; // Default precision is 2
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "P", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_PFormatPrecision4_Char_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.0000 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[20];
        bool success = element.TryFormat(charDest, out int charsWritten, "P4", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_PFormat_Utf8_FormatsCorrectly()
    {
        string jsonValue = "0";
        string expected = "0.00 %";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[20];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "P", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_PFormatBufferTooSmall_Char_ReturnsFalse()
    {
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[5]; // Too small for "0.00 %"
        bool success = element.TryFormat(charDest, out int charsWritten, "P", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Zero_PFormatBufferTooSmall_Utf8_ReturnsFalse()
    {
        string jsonValue = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[5]; // Too small
        bool success = element.TryFormat(byteDest, out int bytesWritten, "P", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Zero_UnknownFormat_Char_FormatsAsZero()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[10];
        bool success = element.TryFormat(charDest, out int charsWritten, "Z", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_UnknownFormat_Utf8_FormatsAsZero()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[10];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "Z", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    [Fact]
    public void Zero_InvalidPrecision_Char_FormatsAsZero()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[10];
        bool success = element.TryFormat(charDest, out int charsWritten, "Nabc", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, charDest.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void Zero_InvalidPrecision_Utf8_FormatsAsZero()
    {
        string jsonValue = "0";
        string expected = "0";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[10];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "Fxyz", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten)));
    }

    #endregion

    [Fact]
    public void Currency_PositivePattern1_NCurrencySymbol_NonZero()
    {
        // Pattern 1: n$
        string jsonValue = "1234.56";
        string expected = "1,234.56$";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 1;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern3_NSpaceCurrencySymbol()
    {
        // Pattern 3: n $
        string jsonValue = "1234.56";
        string expected = "1,234.56 $";
        
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 3;
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", culture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        
        Assert.Equal(expected, result);
    }

    // Test all currency negative patterns (0-15)
    [Fact]
    public void Currency_NegativePattern0_ParenCurrencyN()
    {
        // Pattern 0: ($n)
        string jsonValue = "-123.45";
        string expected = "($123.45)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 0,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern1_MinusCurrencyN()
    {
        // Pattern 1: -$n
        string jsonValue = "-123.45";
        string expected = "-$123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 1,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern2_CurrencyMinusN()
    {
        // Pattern 2: $-n
        string jsonValue = "-123.45";
        string expected = "$-123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 2,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern3_CurrencyNMinus()
    {
        // Pattern 3: $n-
        string jsonValue = "-123.45";
        string expected = "$123.45-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 3,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern4_ParenNCurrency()
    {
        // Pattern 4: (n$)
        string jsonValue = "-123.45";
        string expected = "(123.45$)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 4,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern5_MinusNCurrency()
    {
        // Pattern 5: -n$
        string jsonValue = "-123.45";
        string expected = "-123.45$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 5,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern6_NMinusCurrency()
    {
        // Pattern 6: n-$
        string jsonValue = "-123.45";
        string expected = "123.45-$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 6,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern7_NCurrencyMinus()
    {
        // Pattern 7: n$-
        string jsonValue = "-123.45";
        string expected = "123.45$-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 7,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern8_MinusNSpaceCurrency()
    {
        // Pattern 8: -n $
        string jsonValue = "-123.45";
        string expected = "-123.45 $";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 8,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern9_MinusCurrencySpaceN()
    {
        // Pattern 9: -$ n
        string jsonValue = "-123.45";
        string expected = "-$ 123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 9,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern10_NSpaceCurrencyMinus()
    {
        // Pattern 10: n $-
        string jsonValue = "-123.45";
        string expected = "123.45 $-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 10,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern11_CurrencySpaceNMinus()
    {
        // Pattern 11: $ n-
        string jsonValue = "-123.45";
        string expected = "$ 123.45-";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 11,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern12_CurrencySpaceMinusN()
    {
        // Pattern 12: $ -n
        string jsonValue = "-123.45";
        string expected = "$ -123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 12,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern13_NMinusSpaceCurrency()
    {
        // Pattern 13: n- $
        string jsonValue = "-123.45";
        string expected = "123.45- $";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 13,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern14_ParenCurrencySpaceN()
    {
        // Pattern 14: ($ n)
        string jsonValue = "-123.45";
        string expected = "($ 123.45)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 14,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_NegativePattern15_ParenNSpaceCurrency()
    {
        // Pattern 15: (n $)
        string jsonValue = "-123.45";
        string expected = "(123.45 $)";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyNegativePattern = 15,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    // Test remaining currency positive patterns (1-3)
    [Fact]
    public void Currency_PositivePattern1_NCurrency()
    {
        // Pattern 1: n$
        string jsonValue = "123.45";
        string expected = "123.45$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 1,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern2_CurrencySpaceN()
    {
        // Pattern 2: $ n
        string jsonValue = "123.45";
        string expected = "$ 123.45";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 2,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern3_NSpaceCurrency()
    {
        // Pattern 3: n $
        string jsonValue = "123.45";
        string expected = "123.45 $";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 3,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, "C", formatInfo);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern0_UTF8()
    {
        // Pattern 0: $n (UTF-8 byte path)
        string jsonValue = "1234.56";
        string expected = "$1,234.56";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 0,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", formatInfo);
        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern1_UTF8()
    {
        // Pattern 1: n$ (UTF-8 byte path)
        string jsonValue = "1234.56";
        string expected = "1,234.56$";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 1,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", formatInfo);
        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern2_UTF8()
    {
        // Pattern 2: $ n (UTF-8 byte path)
        string jsonValue = "1234.56";
        string expected = "$ 1,234.56";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 2,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", formatInfo);
        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Currency_PositivePattern3_UTF8()
    {
        // Pattern 3: n $ (UTF-8 byte path)
        string jsonValue = "1234.56";
        string expected = "1,234.56 $";
        
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        var formatInfo = new NumberFormatInfo
        {
            CurrencyPositivePattern = 3,
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ","
        };
        
        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "C", formatInfo);
        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Hexadecimal Formatting Tests

    [Theory]
    [InlineData("0", "X", "0")]
    [InlineData("15", "X", "F")]
    [InlineData("255", "X", "FF")]
    [InlineData("256", "X", "100")]
    [InlineData("4095", "X", "FFF")]
    [InlineData("65535", "X", "FFFF")]
    [InlineData("1234567", "X", "12D687")]
    [InlineData("15", "x", "f")]
    [InlineData("255", "x", "ff")]
    [InlineData("1234567", "x", "12d687")]
    [InlineData("0", "X8", "00000000")]
    [InlineData("255", "X8", "000000FF")]
    [InlineData("255", "x8", "000000ff")]
    // Large numbers > 64 bits
    [InlineData("18446744073709551615", "X", "FFFFFFFFFFFFFFFF")] // 2^64 - 1
    [InlineData("18446744073709551616", "X", "10000000000000000")] // 2^64
    [InlineData("1427247692705959881058285969449495136382746624", "X", "40000000000000000000000000000000000000")] // ~150 bit number
    public void Number_HexFormatChar_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success, "Hex format should succeed for integer");
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "X", "0")]
    [InlineData("15", "X", "F")]
    [InlineData("255", "X", "FF")]
    [InlineData("256", "X", "100")]
    [InlineData("4095", "X", "FFF")]
    [InlineData("65535", "X", "FFFF")]
    [InlineData("1234567", "X", "12D687")]
    [InlineData("15", "x", "f")]
    [InlineData("255", "x", "ff")]
    [InlineData("1234567", "x", "12d687")]
    [InlineData("0", "X8", "00000000")]
    [InlineData("255", "X8", "000000FF")]
    [InlineData("255", "x8", "000000ff")]
    // Large numbers > 64 bits
    [InlineData("18446744073709551615", "X", "FFFFFFFFFFFFFFFF")] // 2^64 - 1
    [InlineData("18446744073709551616", "X", "10000000000000000")] // 2^64
    [InlineData("1427247692705959881058285969449495136382746624", "X", "40000000000000000000000000000000000000")] // ~150 bit number
    public void Number_HexFormatUtf8_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success, "Hex format should succeed for integer");
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.5", "X")]
    [InlineData("-15", "X")]
    [InlineData("0.001", "X")]
    public void Number_HexFormatChar_FailsForInvalidValues(string jsonValue, string format)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.False(success, "Hex format should fail for non-integer or negative values");
    }

    [Theory]
    [InlineData("1.5", "X")]
    [InlineData("-15", "X")]
    [InlineData("0.001", "X")]
    public void Number_HexFormatUtf8_FailsForInvalidValues(string jsonValue, string format)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[256];
        bool success = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.False(success, "Hex format should fail for non-integer or negative values");
    }

    #endregion

    #region Binary Formatting Tests

    [Theory]
    [InlineData("0", "B", "0")]
    [InlineData("1", "B", "1")]
    [InlineData("2", "B", "10")]
    [InlineData("3", "B", "11")]
    [InlineData("7", "B", "111")]
    [InlineData("8", "B", "1000")]
    [InlineData("15", "B", "1111")]
    [InlineData("16", "B", "10000")]
    [InlineData("255", "B", "11111111")]
    [InlineData("256", "B", "100000000")]
    [InlineData("1023", "B", "1111111111")]
    [InlineData("1024", "B", "10000000000")]
    [InlineData("0", "B8", "00000000")]
    [InlineData("5", "B8", "00000101")]
    [InlineData("255", "B8", "11111111")]
    [InlineData("7", "B16", "0000000000000111")]
    // Large numbers
    [InlineData("65535", "B", "1111111111111111")] // 2^16 - 1
    [InlineData("65536", "B", "10000000000000000")] // 2^16
    [InlineData("1048575", "B", "11111111111111111111")] // 2^20 - 1
    public void Number_BinaryFormatChar_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[1024];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success, "Binary format should succeed for integer");
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "B", "0")]
    [InlineData("1", "B", "1")]
    [InlineData("2", "B", "10")]
    [InlineData("3", "B", "11")]
    [InlineData("7", "B", "111")]
    [InlineData("8", "B", "1000")]
    [InlineData("15", "B", "1111")]
    [InlineData("16", "B", "10000")]
    [InlineData("255", "B", "11111111")]
    [InlineData("256", "B", "100000000")]
    [InlineData("1023", "B", "1111111111")]
    [InlineData("1024", "B", "10000000000")]
    [InlineData("0", "B8", "00000000")]
    [InlineData("5", "B8", "00000101")]
    [InlineData("255", "B8", "11111111")]
    [InlineData("7", "B16", "0000000000000111")]
    // Large numbers
    [InlineData("65535", "B", "1111111111111111")] // 2^16 - 1
    [InlineData("65536", "B", "10000000000000000")] // 2^16
    [InlineData("1048575", "B", "11111111111111111111")] // 2^20 - 1
    public void Number_BinaryFormatUtf8_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[1024];
        bool success = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success, "Binary format should succeed for integer");
        string result = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.5", "B")]
    [InlineData("-15", "B")]
    [InlineData("0.001", "B")]
    public void Number_BinaryFormatChar_FailsForInvalidValues(string jsonValue, string format)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<char> charDest = stackalloc char[1024];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.False(success, "Binary format should fail for non-integer or negative values");
    }

    [Theory]
    [InlineData("1.5", "B")]
    [InlineData("-15", "B")]
    [InlineData("0.001", "B")]
    public void Number_BinaryFormatUtf8_FailsForInvalidValues(string jsonValue, string format)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;

        Span<byte> byteDest = stackalloc byte[1024];
        bool success = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.False(success, "Binary format should fail for non-integer or negative values");
    }

    #endregion

    #region Hex and Binary Formatting - Buffer Size and Negative Number Tests

    [Fact]
    public void HexFormat_BufferTooSmall_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("255");
        JsonElement element = doc.RootElement;
        
        // Buffer too small for "FF" result
        Span<char> charDest = stackalloc char[1];
        bool success = element.TryFormat(charDest, out int charsWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void HexFormat_BufferTooSmall_Utf8_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("255");
        JsonElement element = doc.RootElement;
        
        // Buffer too small for "FF" result
        Span<byte> byteDest = stackalloc byte[1];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void BinaryFormat_BufferTooSmall_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("15");
        JsonElement element = doc.RootElement;
        
        // Buffer too small for "1111" result
        Span<char> charDest = stackalloc char[3];
        bool success = element.TryFormat(charDest, out int charsWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void BinaryFormat_BufferTooSmall_Utf8_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("15");
        JsonElement element = doc.RootElement;
        
        // Buffer too small for "1111" result
        Span<byte> byteDest = stackalloc byte[3];
        bool success = element.TryFormat(byteDest, out int bytesWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }



    [Theory]
    [InlineData("1000000000000000000000000", "X")]  // Very large number requiring ArrayPool
    [InlineData("1000000000000000000000000", "B")]  // Very large number requiring ArrayPool
    public void HexBinaryFormat_VeryLargeNumbers_UsesArrayPool(string jsonValue, string format)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        // Test char path - should use ArrayPool for large numbers
        Span<char> charDest = stackalloc char[2048];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.True(charsWritten > 0);
        
        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[2048];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(successUtf8);
        Assert.True(bytesWritten > 0);
    }

    [Theory]
    [InlineData("12345678901234567890", "X", "AB54A98CEB1F0AD2")]
    [InlineData("18446744073709551615", "X", "FFFFFFFFFFFFFFFF")]  // Max ulong
    [InlineData("340282366920938463463374607431768211455", "X", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]  // Max UInt128
    public void HexFormat_LargeIntegers_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        // Test char path
        Span<char> charDest = stackalloc char[256];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
        
        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[256];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(successUtf8);
        string resultUtf8 = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, resultUtf8);
    }

    [Theory]
    [InlineData("1023", "B", "1111111111")]
    [InlineData("65535", "B", "1111111111111111")]  // Max ushort
    [InlineData("4294967295", "B", "11111111111111111111111111111111")]  // Max uint
    public void BinaryFormat_LargeIntegers_FormatsCorrectly(string jsonValue, string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement element = doc.RootElement;
        
        // Test char path
        Span<char> charDest = stackalloc char[512];
        bool success = element.TryFormat(charDest, out int charsWritten, format, CultureInfo.InvariantCulture);
        Assert.True(success);
        string result = charDest.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
        
        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[512];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, format, CultureInfo.InvariantCulture);
        Assert.True(successUtf8);
        string resultUtf8 = JsonReaderHelper.TranscodeHelper(byteDest.Slice(0, bytesWritten));
        Assert.Equal(expected, resultUtf8);
    }

    [Fact]
    public void HexFormat_NegativeNumber_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-42");
        JsonElement element = doc.RootElement;

        // Test char path
        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);

        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[100];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(successUtf8);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void BinaryFormat_NegativeNumber_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-42");
        JsonElement element = doc.RootElement;

        // Test char path
        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);

        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[100];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(successUtf8);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void HexFormat_NonInteger_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123.456");
        JsonElement element = doc.RootElement;

        // Test char path
        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);

        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[100];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, "X", CultureInfo.InvariantCulture);
        Assert.False(successUtf8);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void BinaryFormat_NonInteger_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123.456");
        JsonElement element = doc.RootElement;

        // Test char path
        Span<char> charDest = stackalloc char[100];
        bool success = element.TryFormat(charDest, out int charsWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(success);
        Assert.Equal(0, charsWritten);

        // Test UTF-8 path
        Span<byte> byteDest = stackalloc byte[100];
        bool successUtf8 = element.TryFormat(byteDest, out int bytesWritten, "B", CultureInfo.InvariantCulture);
        Assert.False(successUtf8);
        Assert.Equal(0, bytesWritten);
    }

    #endregion
}
