using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementHelpersParseNumberTests
{
    [TestMethod]
    [DataRow("1e-3", false, "1", "", -3)]
    [DataRow("1.0e-3", false, "1", "", -3)]
    [DataRow("0.1e1", false, "", "1", 0)]
    [DataRow("0.01e2", false, "", "1", 0)]
    public void ParseNumber_HandlesExponentAndFractional(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.AreEqual(expectedNegative, isNegative);
        Assert.AreEqual(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.AreEqual(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.AreEqual(expectedExponent, exponent);
    }

    [TestMethod]
    [DataRow("0.0000", false, "", "", 0)]
    [DataRow("1.000e2", false, "1", "", 2)]
    [DataRow("1.2300e-2", false, "1", "23", -4)]
    public void ParseNumber_NormalizesZeros(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.AreEqual(expectedNegative, isNegative);
        Assert.AreEqual(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.AreEqual(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.AreEqual(expectedExponent, exponent);
    }

    [TestMethod]
    [DataRow("0", false, "", "", 0)]
    [DataRow("123", false, "123", "", 0)]
    [DataRow("-456", true, "456", "", 0)]
    [DataRow("789.01", false, "789", "01", -2)]
    [DataRow("-0.00123", true, "", "123", -5)]
    [DataRow("1e3", false, "1", "", 3)]
    [DataRow("1.23e2", false, "1", "23", 0)]
    [DataRow("0.000", false, "", "", 0)]
    [DataRow("1000", false, "1", "", 3)]
    [DataRow("0.1000", false, "", "1", -1)]
    [DataRow("1.2300e2", false, "1", "23", 0)]
    [DataRow("0.000123", false, "", "123", -6)]
    [DataRow("-0", false, "", "", 0)]
    public void ParseNumber_ParsesCorrectly(string jsonNumber, bool expectedNegative, string expectedIntegral, string expectedFractional, int expectedExponent)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.AreEqual(expectedNegative, isNegative);
        Assert.AreEqual(expectedIntegral, JsonHelpers.Utf8GetString(integral));
        Assert.AreEqual(expectedFractional, JsonHelpers.Utf8GetString(fractional));
        Assert.AreEqual(expectedExponent, exponent);
    }
}
