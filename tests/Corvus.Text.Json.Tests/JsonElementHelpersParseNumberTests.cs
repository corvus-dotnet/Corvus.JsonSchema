using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersParseNumberTests
{
    [Theory]
    [InlineData("1e-3", false, "1", "", -3)]
    [InlineData("1.0e-3", false, "1", "", -3)]
    [InlineData("0.1e1", false, "", "1", 0)]
    [InlineData("0.01e2", false, "", "1", 0)]
    public void ParseNumber_HandlesExponentAndFractional(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.Equal(expectedNegative, isNegative);
        Assert.Equal(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.Equal(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.Equal(expectedExponent, exponent);
    }

    [Theory]
    [InlineData("0.0000", false, "", "", 0)]
    [InlineData("1.000e2", false, "1", "", 2)]
    [InlineData("1.2300e-2", false, "1", "23", -4)]
    public void ParseNumber_NormalizesZeros(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.Equal(expectedNegative, isNegative);
        Assert.Equal(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.Equal(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.Equal(expectedExponent, exponent);
    }

    [Theory]
    [InlineData("0", false, "", "", 0)]
    [InlineData("123", false, "123", "", 0)]
    [InlineData("-456", true, "456", "", 0)]
    [InlineData("789.01", false, "789", "01", -2)]
    [InlineData("-0.00123", true, "", "123", -5)]
    [InlineData("1e3", false, "1", "", 3)]
    [InlineData("1.23e2", false, "1", "23", 0)]
    [InlineData("0.000", false, "", "", 0)]
    [InlineData("1000", false, "1", "", 3)]
    [InlineData("0.1000", false, "", "1", -1)]
    [InlineData("1.2300e2", false, "1", "23", 0)]
    [InlineData("0.000123", false, "", "123", -6)]
    [InlineData("-0", false, "", "", 0)]
    public void ParseNumber_ParsesCorrectly(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.Equal(expectedNegative, isNegative);
        Assert.Equal(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.Equal(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.Equal(expectedExponent, exponent);
    }
}