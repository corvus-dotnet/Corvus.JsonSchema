using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersTryFormatPercentTests
{
    [Theory]
    [InlineData("0.5", "50.00 %")]
    [InlineData("0.25", "25.00 %")]
    [InlineData("1", "100.00 %")]
    [InlineData("1.5", "150.00 %")]
    [InlineData("0.12345", "12.35 %")]
    public void TryFormatPercent_BasicValues_MultipliesBy100(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1, // Use default precision
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.12345", 0, "12 %")]
    [InlineData("0.12345", 1, "12.3 %")]
    [InlineData("0.12345", 2, "12.35 %")]
    [InlineData("0.12345", 3, "12.345 %")]
    [InlineData("0.12345", 4, "12.3450 %")]
    public void TryFormatPercent_WithPrecision_FormatsToSpecifiedDecimalPlaces(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-0.5", "-50.00 %")]
    [InlineData("-0.25", "-25.00 %")]
    [InlineData("-1.5", "-150.00 %")]
    public void TryFormatPercent_WithNegativeNumbers_IncludesNegativeSign(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatPercent_WithGrouping_InsertsGroupSeparators()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("12.345");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = new[] { 3 },
            PercentPositivePattern = 0 // n %
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal("1,234.50 %", result);
    }

    [Fact]
    public void TryFormatPercent_UsesCustomPercentSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "pct",
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 1 // n%
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal("50.00pct", result);
    }

    [Theory]
    [InlineData(0, "50.00 %")]  // n %
    [InlineData(1, "50.00%")]   // n%
    [InlineData(2, "%50.00")]   // %n
    [InlineData(3, "% 50.00")]  // % n
    public void TryFormatPercent_RespectsPositivePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentPositivePattern = pattern
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "-50.00 %")]   // -n %
    [InlineData(1, "-50.00%")]    // -n%
    [InlineData(2, "-%50.00")]    // -%n
    [InlineData(3, "%-50.00")]    // %-n
    [InlineData(4, "%50.00-")]    // %n-
    [InlineData(5, "50.00-%")]    // n-%
    [InlineData(6, "50.00%-")]    // n%-
    [InlineData(7, "-% 50.00")]   // -% n
    [InlineData(8, "50.00 %-")]   // n %-
    [InlineData(9, "% 50.00-")]   // % n-
    [InlineData(10, "% -50.00")]  // % -n
    [InlineData(11, "50.00- %")]  // n- %
    public void TryFormatPercent_RespectsNegativePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentNegativePattern = pattern
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatPercent_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[3];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("0.12995", 2, "13.00 %")]  // Rounds up
    [InlineData("0.12994", 2, "12.99 %")]  // Rounds down
    [InlineData("0.99995", 2, "100.00 %")] // Rounds to 100
    public void TryFormatPercent_RoundsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5)]   // -n %
    [InlineData(1, 5)]   // -n%
    [InlineData(2, 5)]   // -%n
    [InlineData(3, 5)]   // %-n
    [InlineData(4, 5)]   // %n-
    [InlineData(5, 5)]   // n-%
    [InlineData(6, 5)]   // n%-
    [InlineData(7, 5)]   // -% n
    [InlineData(8, 5)]   // n %-
    [InlineData(9, 5)]   // % n-
    [InlineData(10, 5)]  // % -n
    [InlineData(11, 5)]  // n- %
    public void TryFormatPercent_NegativePattern_BufferTooSmall(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentNegativePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData(0, 5)]   // n %
    [InlineData(1, 5)]   // n%
    [InlineData(2, 5)]   // %n
    [InlineData(3, 5)]   // % n
    public void TryFormatPercent_PositivePattern_BufferTooSmall(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentPositivePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Tests with buffer just 1 char less than the expected output, to hit the LATE overflow guards
    /// (after the number part has been written, but suffix doesn't fit).
    /// All patterns below have explicit overflow guards after the number part.
    /// </summary>
    [Theory]
    [InlineData(0, 7)]   // -n % → "-50.00 %" = 8 chars, buffer 7 hits guard at pos+1+%len > dest.Length
    [InlineData(1, 6)]   // -n%  → "-50.00%" = 7 chars, buffer 6 hits guard at pos+%len > dest.Length
    [InlineData(4, 6)]   // %n-  → "%50.00-" = 7 chars, buffer 6 hits guard at pos+negLen > dest.Length
    [InlineData(5, 6)]   // n-%  → "50.00-%" = 7 chars, buffer 6 hits guard at pos+neg+%len > dest.Length
    [InlineData(6, 6)]   // n%-  → "50.00%-" = 7 chars, buffer 6 hits guard at pos+%+negLen > dest.Length
    [InlineData(8, 7)]   // n %- → "50.00 %-" = 8 chars, buffer 7 hits guard at pos+1+%+negLen > dest.Length
    [InlineData(9, 7)]   // % n- → "% 50.00-" = 8 chars, buffer 7 hits guard at pos+negLen > dest.Length
    [InlineData(11, 7)]  // n- % → "50.00- %" = 8 chars, buffer 7 hits guard at pos+neg+1+%len > dest.Length
    public void TryFormatPercent_NegativePattern_BufferOneLessThanNeeded(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentNegativePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Tests positive patterns with buffer just 1 char less than needed.
    /// Patterns 0 and 1 have guards after the number part.
    /// Pattern 3 has a guard for the space after %.
    /// </summary>
    [Theory]
    [InlineData(0, 6)]   // n %  → "50.00 %" = 7 chars, buffer 6 hits pos+1+%len > dest
    [InlineData(1, 5)]   // n%   → "50.00%" = 6 chars, buffer 5 hits pos+%len > dest
    [InlineData(3, 2)]   // % n  → "% 50.00" = 7 chars, buffer 2 hits pos+1 > dest after %
    public void TryFormatPercent_PositivePattern_BufferOneLessThanNeeded(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentPositivePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }
}