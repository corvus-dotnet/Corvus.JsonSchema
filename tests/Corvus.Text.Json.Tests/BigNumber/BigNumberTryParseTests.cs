// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber.TryParse methods.
/// </summary>
public class BigNumberTryParseTests
{
    [Fact]
    public void TryParse_WithZero_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("0");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        BigNumberTestData.AssertParseResult(success, result, BigInteger.Zero, 0, "0");
    }

    [Fact]
    public void TryParse_WithPositiveInteger_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("123");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        BigNumberTestData.AssertParseResult(success, result, new BigInteger(123), 0, "123");
    }

    [Fact]
    public void TryParse_WithNegativeInteger_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("-456");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        BigNumberTestData.AssertParseResult(success, result, new BigInteger(-456), 0, "-456");
    }

    [Fact]
    public void TryParse_WithDecimalNumber_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("123.456");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.True(success);
        Assert.Equal(new BigInteger(123456), result.Significand);
        Assert.Equal(-3, result.Exponent);
    }

    [Fact]
    public void TryParse_WithScientificNotation_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("1.23e4");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.True(success);
        Assert.Equal(new BigInteger(123), result.Significand);
        Assert.Equal(2, result.Exponent);
    }

    [Fact]
    public void TryParse_WithLargeNumber_ShouldParseCorrectly()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("999999999999999999999999999999");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.True(success);
        Assert.Equal(BigInteger.Parse("999999999999999999999999999999"), result.Significand);
        Assert.Equal(0, result.Exponent);
    }

    [Fact]
    public void TryParse_WithEmptyInput_ShouldReturnFalse()
    {
        // Arrange
        ReadOnlySpan<byte> input = ReadOnlySpan<byte>.Empty;

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryParse_WithInvalidInput_ShouldReturnFalse()
    {
        // Arrange
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("abc");

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.ParseData), MemberType = typeof(BigNumberTestData))]
    public void TryParse_TheoryTest_WithValidInputs_ShouldParseCorrectly(
        string input, bool expectedSuccess, BigInteger expectedSignificand, int expectedExponent)
    {
        // Arrange
        ReadOnlySpan<byte> inputSpan = Encoding.UTF8.GetBytes(input);

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(inputSpan, out BigNumber result);

        // Assert
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            BigNumberTestData.AssertParseResult(success, result, expectedSignificand, expectedExponent, input);
        }
    }

    [Fact]
    public void TryParse_RoundTripTest_ShouldPreserveValue()
    {
        // Arrange
        IEnumerable<BigNumber> originalNumbers = BigNumberTestData.GetTestNumbers();

        // Allocate buffer outside the loop
        Span<char> charBuffer = stackalloc char[1000];

        foreach (BigNumber original in originalNumbers)
        {
            // Format the number to a string
            bool formatSuccess = original.TryFormat(charBuffer, out int charsWritten);
            Assert.True(formatSuccess);

            ReadOnlySpan<char> formattedString = charBuffer.Slice(0, charsWritten);
            ReadOnlySpan<byte> formattedInput = Encoding.UTF8.GetBytes(formattedString.ToString());

            // Act - parse it back
            bool parseSuccess = Corvus.Numerics.BigNumber.TryParse(formattedInput, out BigNumber parsedBigNumber);

            // Assert
            Assert.True(parseSuccess);
            BigNumberTestData.AssertBigNumbersEqual(original, parsedBigNumber,
                $"Round-trip failed for {formattedString.ToString()}");
        }
    }
}