// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber.ToString() method.
/// </summary>
public class BigNumberToStringTests
{
    [Fact]
    public void ToString_WithZeroSignificandZeroExponent_ShouldReturnZero()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("0", result);
    }

    [Fact]
    public void ToString_WithPositiveSignificandZeroExponent_ShouldReturnSignificand()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123", result);
    }

    [Fact]
    public void ToString_WithNegativeSignificandZeroExponent_ShouldReturnSignificand()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 0);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("-456", result);
    }

    [Fact]
    public void ToString_WithZeroExponentPositiveSignificand_ShouldNotIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(789), 0);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("789", result);
        Assert.DoesNotContain("E", result);
    }

    [Fact]
    public void ToString_WithPositiveExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E5", result);
    }

    [Fact]
    public void ToString_WithNegativeExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E-5", result);
    }

    [Fact]
    public void ToString_WithVeryLargeSignificand_ShouldReturnCorrectString()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
        var bigNumber = new Corvus.Numerics.BigNumber(largeSignificand, 0);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("1234567890123456789012345678901234567890123456789E1", result);
    }

    [Fact]
    public void ToString_WithVeryLargeExponent_ShouldReturnCorrectString()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 999999);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E999999", result);
    }

    [Fact]
    public void ToString_WithVeryLargeNegativeExponent_ShouldReturnCorrectString()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -999999);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E-999999", result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.FormatData), MemberType = typeof(BigNumberTestData))]
    public void ToString_WithVariousInputs_ShouldReturnCorrectString(
        BigInteger significand, int exponent, string expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToString_WithNegativeSignificandAndPositiveExponent_ShouldReturnCorrectString()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), 10);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("-789E10", result);
    }

    [Fact]
    public void ToString_WithNegativeSignificandAndNegativeExponent_ShouldReturnCorrectString()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), -10);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("-789E-10", result);
    }

    [Fact]
    public void ToString_WithZeroSignificandAndNonZeroExponent_ShouldReturnCorrectString()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 42);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("0", result);
    }

    [Fact]
    public void ToString_CompareWithTryFormatChar_ShouldProduceSameResult()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-98765), 12345);
        Span<char> charBuffer = stackalloc char[50];

        // Act
        string toStringResult = bigNumber.ToString();
        bool tryFormatSuccess = bigNumber.TryFormat(charBuffer, out int charsWritten);
        string tryFormatResult = charBuffer.Slice(0, charsWritten).ToString();

        // Assert
        Assert.True(tryFormatSuccess);
        Assert.Equal(toStringResult, tryFormatResult);
    }

    [Fact]
    public void ToString_CompareWithTryFormatByte_ShouldProduceSameResult()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-98765), 12345);
        Span<byte> byteBuffer = stackalloc byte[50];

        // Act
        string toStringResult = bigNumber.ToString();
        bool tryFormatSuccess = bigNumber.TryFormat(byteBuffer, out int bytesWritten);
        string tryFormatResult = Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten).ToArray());

        // Assert
        Assert.True(tryFormatSuccess);
        Assert.Equal(toStringResult, tryFormatResult);
    }

    [Fact]
    public void ToString_RoundTripWithParse_ShouldProduceSameResult()
    {
        // Arrange
        var originalBigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), 123);

        // Act
        string stringResult = originalBigNumber.ToString();
        bool parseSuccess = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(stringResult), out BigNumber parsedBigNumber);

        // Assert
        Assert.True(parseSuccess);
        BigNumberTestData.AssertBigNumbersEqual(originalBigNumber, parsedBigNumber);
    }

    [Fact]
    public void ToString_MultipleCallsOnSameInstance_ShouldReturnSameValue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act
        string result1 = bigNumber.ToString();
        string result2 = bigNumber.ToString();
        string result3 = bigNumber.ToString();

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    [Fact]
    public void ToString_WithDefaultStruct_ShouldReturnZero()
    {
        // Arrange
        var bigNumber = default(Corvus.Numerics.BigNumber);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("0", result);
    }

    [Fact]
    public void ToString_AfterNormalization_ShouldBeConsistent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        BigNumber normalizedBigNumber = bigNumber.Normalize();

        // Act
        string originalResult = bigNumber.ToString();
        string normalizedResult = normalizedBigNumber.ToString();

        // Assert
        // The string representation should be the same before and after normalization
        // if normalization doesn't change the mathematical value representation
        Assert.Equal(originalResult, normalizedResult);
    }

    [Fact]
    public void ToString_WithVeryLargeNumber_ShouldNotThrow()
    {
        // Arrange
        var veryLargeSignificand = BigInteger.Parse(new string('9', 1000)); // 1000 nines
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeSignificand, 999999);

        // Act & Assert
        string result = bigNumber.ToString();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.StartsWith(new string('9', 1000), result);
        Assert.Contains("E999999", result);
    }

    [Fact]
    public void ToString_WithMinimumValues_ShouldWork()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-1), int.MinValue);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal($"-1E{int.MinValue}", result);
    }

    [Fact]
    public void ToString_WithMaximumValues_ShouldWork()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.One, int.MaxValue);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal($"1E{int.MaxValue}", result);
    }

    [Fact]
    public void ToString_ConsistentWithObjectToString_ShouldBeTrue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        object objectBigNumber = bigNumber;

        // Act
        string directResult = bigNumber.ToString();
        string objectResult = objectBigNumber.ToString();

        // Assert
        Assert.Equal(directResult, objectResult);
    }

    [Fact]
    public void ToString_ExponentOfOne_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 1);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E1", result);
    }

    [Fact]
    public void ToString_ExponentOfMinusOne_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -1);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("123E-1", result);
    }

    [Fact]
    public void ToString_SignificandOfOne_ShouldWork()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.One, 5);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("1E5", result);
    }

    [Fact]
    public void ToString_SignificandOfMinusOne_ShouldWork()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.MinusOne, 5);

        // Act
        string result = bigNumber.ToString();

        // Assert
        Assert.Equal("-1E5", result);
    }
}