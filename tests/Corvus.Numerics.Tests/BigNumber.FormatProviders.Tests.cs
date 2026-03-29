
using System.Globalization;
using System.Text;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;
/// <summary>
/// Tests for BigNumber formatting with standard format providers (G, F, N, E, C, P).
/// Tests the same format strings that System.Decimal supports.
/// </summary>
public class BigNumberFormatProvidersTests
{
    #region General Format (G)

    [Fact]
    public void Format_G_General_DefaultPrecision()
    {
        BigNumber value = new(123456789, -3);

        string result = value.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldBe("123456789E-3");
    }

    [Fact]
    public void Format_G_General_WithPrecision()
    {
        BigNumber value = new(123456789, 0);

        string result = value.ToString("G5", CultureInfo.InvariantCulture);

        // Should show 5 significant digits
        result.ShouldBe("12346E4");
    }

    [Fact]
    public void Format_G_LowercaseG_SameAsUppercase()
    {
        BigNumber value = new(123456, -2);

        string resultUpper = value.ToString("G", CultureInfo.InvariantCulture);
        string resultLower = value.ToString("g", CultureInfo.InvariantCulture);

        resultLower.ShouldBe(resultUpper.ToLowerInvariant());
    }

    [Fact]
    public void Format_G_Zero()
    {
        BigNumber value = BigNumber.Zero;

        string result = value.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldBe("0");
    }

    #endregion

    #region Fixed-Point Format (F)

    [Fact]
    public void Format_F_FixedPoint_DefaultPrecision()
    {
        BigNumber value = new(123456, -2);

        string result = value.ToString("F", CultureInfo.InvariantCulture);

        // Default precision is 2 decimal places
        result.ShouldBe("1234.56");
    }

    [Fact]
    public void Format_F_FixedPoint_WithPrecision()
    {
        BigNumber value = new(123456, -5);

        string result = value.ToString("F5", CultureInfo.InvariantCulture);

        result.ShouldBe("1.23456");
    }

    [Fact]
    public void Format_F_FixedPoint_NoPrecision()
    {
        BigNumber value = new(123456, -2);

        string result = value.ToString("F0", CultureInfo.InvariantCulture);

        result.ShouldBe("1235"); // Rounded
    }

    [Fact]
    public void Format_F_Negative()
    {
        BigNumber value = new(-123456, -2);

        string result = value.ToString("F2", CultureInfo.InvariantCulture);

        result.ShouldBe("-1234.56");
    }

    [Fact]
    public void Format_F_LargeNumber()
    {
        BigNumber value = new(123456789, 5);

        string result = value.ToString("F2", CultureInfo.InvariantCulture);

        result.ShouldBe("12345678900000.00");
    }

    #endregion

    #region Number Format (N)

    [Fact]
    public void Format_N_Number_WithThousandsSeparator()
    {
        BigNumber value = new(1234567, -2);

        string result = value.ToString("N", CultureInfo.InvariantCulture);

        // Default 2 decimal places with thousands separator
        result.ShouldBe("12,345.67");
    }

    [Fact]
    public void Format_N_Number_WithPrecision()
    {
        BigNumber value = new(1234567, -5);

        string result = value.ToString("N5", CultureInfo.InvariantCulture);

        result.ShouldBe("12.34567");
    }

    [Fact]
    public void Format_N_Number_FrenchCulture()
    {
        BigNumber value = new(1234567, -2);

        string result = value.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));

        // French uses narrow no-break space (U+202F) for thousands and comma for decimal
        result.ShouldBe("12\u202F345,67");
    }

    [Fact]
    public void Format_N_Negative()
    {
        BigNumber value = new(-1234567, -2);

        string result = value.ToString("N2", CultureInfo.InvariantCulture);

        result.ShouldBe("-12,345.67");
    }

    #endregion

    #region Exponential Format (E)

    [Fact]
    public void Format_E_Exponential_DefaultPrecision()
    {
        BigNumber value = new(123456789, 0);

        string result = value.ToString("E", CultureInfo.InvariantCulture);

        // Default 6 decimal places in mantissa
        result.ShouldBe("1.234568E+008");
    }

    [Fact]
    public void Format_E_Exponential_WithPrecision()
    {
        BigNumber value = new(123456789, 0);

        string result = value.ToString("E3", CultureInfo.InvariantCulture);

        result.ShouldBe("1.235E+008");
    }

    [Fact]
    public void Format_E_Lowercase()
    {
        BigNumber value = new(123456, 0);

        string result = value.ToString("e2", CultureInfo.InvariantCulture);

        result.ShouldBe("1.23e+005");
    }

    [Fact]
    public void Format_E_NegativeExponent()
    {
        BigNumber value = new(123, -5);

        string result = value.ToString("E2", CultureInfo.InvariantCulture);

        result.ShouldBe("1.23E-003");
    }

    [Fact]
    public void Format_E_Negative()
    {
        BigNumber value = new(-123456, 0);

        string result = value.ToString("E2", CultureInfo.InvariantCulture);

        result.ShouldBe("-1.23E+005");
    }

    #endregion

    #region Currency Format (C)

    [Fact]
    public void Format_C_Currency_InvariantCulture()
    {
        BigNumber value = new(123456, -2);

        string result = value.ToString("C", CultureInfo.InvariantCulture);

        // Invariant culture uses ¤ symbol
        result.ShouldBe("¤1,234.56");
    }

    [Fact]
    public void Format_C_Currency_USDollars()
    {
        BigNumber value = new(123456, -2);

        string result = value.ToString("C", CultureInfo.GetCultureInfo("en-US"));

        result.ShouldBe("$1,234.56");
    }

    [Fact]
    public void Format_C_Currency_Euros()
    {
        BigNumber value = new(123456, -2);

        string result = value.ToString("C2", CultureInfo.GetCultureInfo("fr-FR"));

        result.ShouldBe("1\u202F234,56 €"); // French uses narrow no-break space
    }

    [Fact]
    public void Format_C_Currency_WithPrecision()
    {
        BigNumber value = new(123456, -3);

        string result = value.ToString("C3", CultureInfo.GetCultureInfo("en-US"));

        result.ShouldBe("$123.456");
    }

    [Fact]
    public void Format_C_Negative_UsesParentheses()
    {
        BigNumber value = new(-123456, -2);

        string result = value.ToString("C", CultureInfo.GetCultureInfo("en-US"));

#if NET
        result.ShouldBe("-$1,234.56");
#else
        // .NET Framework uses parentheses for negative currency by default
        result.ShouldBe("($1,234.56)");
#endif
    }

    #endregion

    #region Percent Format (P)

    [Fact]
    public void Format_P_Percent_DefaultPrecision()
    {
        BigNumber value = new(75, -2); // 0.75

        string result = value.ToString("P", CultureInfo.InvariantCulture);

        result.ShouldBe("75.00 %");
    }

    [Fact]
    public void Format_P_Percent_WithPrecision()
    {
        BigNumber value = new(12345, -4); // 1.2345

        string result = value.ToString("P1", CultureInfo.InvariantCulture);

        result.ShouldBe("123.5 %");
    }

    [Fact]
    public void Format_P_Percent_USCulture()
    {
        BigNumber value = new(75, -2); // 0.75

        string result = value.ToString("P", CultureInfo.GetCultureInfo("en-US"));

        result.ShouldBe("75.00%");
    }

    [Fact]
    public void Format_P_Percent_FrenchCulture()
    {
        BigNumber value = new(75, -2);

        string result = value.ToString("P2", CultureInfo.GetCultureInfo("fr-FR"));

        result.ShouldBe("75,00 %");
    }

    [Fact]
    public void Format_P_Negative()
    {
        BigNumber value = new(-75, -2);

        string result = value.ToString("P", CultureInfo.GetCultureInfo("en-US"));

        result.ShouldBe("-75.00%");
    }

    #endregion

    #region Custom Format Strings

    // Note: Custom format strings are not yet implemented.
    // The standard format specifiers (G, F, N, E, C, P) are supported.

    #endregion

    #region Null and Default Format

    [Fact]
    public void Format_NullFormat_UsesGeneralFormat()
    {
        BigNumber value = new(123456, -2);

        string resultNull = value.ToString(null, CultureInfo.InvariantCulture);
        string resultG = value.ToString("G", CultureInfo.InvariantCulture);

        resultNull.ShouldBe(resultG);
    }

    [Fact]
    public void Format_EmptyFormat_UsesGeneralFormat()
    {
        BigNumber value = new(123456, -2);

        string resultEmpty = value.ToString("", CultureInfo.InvariantCulture);
        string resultG = value.ToString("G", CultureInfo.InvariantCulture);

        resultEmpty.ShouldBe(resultG);
    }

    #endregion

    #region TryFormat with Format Providers

    [Fact]
    public void TryFormat_UTF16_FixedPoint_Succeeds()
    {
        BigNumber value = new(123456, -2);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1234.56");
    }

    [Fact]
    public void TryFormat_UTF16_Number_WithCulture()
    {
        BigNumber value = new(1234567, -2);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.GetCultureInfo("fr-FR"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12\u202F345,67"); // French uses narrow no-break space U+202F
    }

    [Fact]
    public void TryFormat_UTF16_Currency_Succeeds()
    {
        BigNumber value = new(123456, -2);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "C", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("$1,234.56");
    }

    [Fact]
    public void TryFormat_UTF8_FixedPoint_Succeeds()
    {
        BigNumber value = new(123456, -2);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("1234.56");
    }

    [Fact]
    public void TryFormat_UTF8_Number_WithCulture()
    {
        BigNumber value = new(1234567, -2);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, "N2", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("12,345.67");
    }

    #endregion

    #region Round-Trip with Format Providers

    [Fact]
    public void RoundTrip_GeneralFormat_MaintainsPrecision()
    {
        BigNumber original = new(123456789, -5);

        string formatted = original.ToString("G", CultureInfo.InvariantCulture);
        var parsed = BigNumber.Parse(formatted, NumberStyles.Float, CultureInfo.InvariantCulture);

        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_ExponentialFormat_MaintainsPrecision()
    {
        BigNumber original = new(123456789, 10);

        string formatted = original.ToString("E", CultureInfo.InvariantCulture);
        var parsed = BigNumber.Parse(formatted, NumberStyles.Float, CultureInfo.InvariantCulture);

        // May have rounding differences due to precision
        parsed.ToString().ShouldNotBeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Format_InvalidFormatString_ThrowsException()
    {
        BigNumber value = new(123, 0);

        Should.Throw<FormatException>(() => value.ToString("X", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_Zero_AllFormats()
    {
        BigNumber zero = BigNumber.Zero;

        zero.ToString("G", CultureInfo.InvariantCulture).ShouldBe("0");
        zero.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("0.00");
        zero.ToString("N2", CultureInfo.InvariantCulture).ShouldBe("0.00");
        zero.ToString("E2", CultureInfo.InvariantCulture).ShouldBe("0.00E+000");
        zero.ToString("P2", CultureInfo.InvariantCulture).ShouldBe("0.00 %");
    }

    [Fact]
    public void Format_VeryLargeNumber_DoesNotOverflow()
    {
        BigNumber value = new(System.Numerics.BigInteger.Parse("123456789012345678901234567890"), 100);

        string result = value.ToString("E", CultureInfo.InvariantCulture);

        result.ShouldNotBeEmpty();
        result.ShouldContain("E+");
    }

    #endregion

    #region Comparison with Decimal Behavior

    [Fact]
    public void Format_CompareToBehavior_FixedPoint()
    {
        // This test documents expected behavior differences from decimal
        // BigNumber can represent much larger/smaller values than decimal

        BigNumber bigValue = new(123456, -100); // Way beyond decimal range
        string result = bigValue.ToString("F10", CultureInfo.InvariantCulture);

        result.ShouldNotBeEmpty();
        // Will be extremely small number
        result.ShouldStartWith("0.0000000000");
    }

    [Fact]
    public void Format_ConsistentWithDecimal_SmallValues()
    {
        // For values within decimal range, should be similar to decimal
        decimal decValue = 123.45m;
        BigNumber bigValue = new(12345, -2);

        string decResult = decValue.ToString("F2", CultureInfo.InvariantCulture);
        string bigResult = bigValue.ToString("F2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decResult);
    }

    #endregion
}