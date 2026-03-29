// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber.TryFormat(Span&lt;byte&gt;) method.
/// </summary>
public class BigNumberTryFormatByteTests
{
    [Fact]
    public void TryFormat_ToByteSpan_WithZeroSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        Span<byte> buffer = stackalloc byte[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "0");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithPositiveSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        Span<byte> buffer = stackalloc byte[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithNegativeSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 0);
        Span<byte> buffer = stackalloc byte[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "-456");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithZeroExponentPositiveSignificand_ShouldNotIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(789), 0);
        Span<byte> buffer = stackalloc byte[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "789");
        Assert.DoesNotContain("E", result);
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithPositiveExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E5");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithNegativeExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E-5");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithVeryLargeSignificand_ShouldFormatCorrectly()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
        var bigNumber = new Corvus.Numerics.BigNumber(largeSignificand, 0);
        Span<byte> buffer = stackalloc byte[100];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result,
            "1234567890123456789012345678901234567890123456789E1");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithVeryLargeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 999999);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E999999");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithVeryLargeNegativeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -999999);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E-999999");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithInsufficientBufferSize_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<byte> buffer = stackalloc byte[4]; // Too small for "123E5"

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);

        // Assert
        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithExactBufferSize_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<byte> buffer = stackalloc byte[5]; // Exact size for "123E5"
        buffer.Clear(); // Initialize to ensure we only write what we expect

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E5");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithExcessiveBufferSize_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<byte> buffer = stackalloc byte[100]; // Much larger than needed

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "123E5");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithZeroLengthSpan_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        Span<byte> buffer = Span<byte>.Empty;

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);

        // Assert
        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.FormatData), MemberType = typeof(BigNumberTestData))]
    public void TryFormat_ToByteSpan_WithVariousInputs_ShouldFormatCorrectly(
        BigInteger significand, int exponent, string expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);
        Span<byte> buffer = stackalloc byte[200]; // Large enough for any test case

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, expected,
            $"BigNumber({significand}, {exponent})");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithNegativeSignificandAndPositiveExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), 10);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "-789E10");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithNegativeSignificandAndNegativeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), -10);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "-789E-10");
    }

    [Fact]
    public void TryFormat_ToByteSpan_WithZeroSignificandAndNonZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 42);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        BigNumberTestData.AssertFormatResult(success, bytesWritten, result, "0");
    }

    [Fact]
    public void TryFormat_ToByteSpan_SignificandRequiringMaxFormatLength_ShouldWork()
    {
        // Arrange - Create a very large significand to test buffer limits
        var veryLargeSignificand = BigInteger.Parse(new string('9', 100)); // 100 nines
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeSignificand, 999);
        Span<byte> buffer = stackalloc byte[200]; // Large buffer

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        string result = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());

        // Assert
        Assert.True(success, "Should successfully format very large number");
        Assert.True(bytesWritten > 100, "Should write more than 100 bytes");
        Assert.StartsWith(new string('9', 100), result);
        Assert.Contains("E999", result);
    }

    [Fact]
    public void TryFormat_ToByteSpan_UTF8Encoding_ShouldBeCorrect()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -456);
        Span<byte> buffer = stackalloc byte[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int bytesWritten);
        byte[] resultBytes = buffer.Slice(0, bytesWritten).ToArray();
        byte[] expectedBytes = BigNumberTestData.GetUtf8Bytes("123E-456");

        // Assert
        Assert.True(success);
        Assert.Equal(expectedBytes.Length, bytesWritten);
        Assert.True(resultBytes.AsSpan().SequenceEqual(expectedBytes));
    }

    [Fact]
    public void TryFormat_ToByteSpan_CompareWithCharSpan_ShouldProduceSameResult()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-98765), 12345);
        Span<byte> byteBuffer = stackalloc byte[50];
        Span<char> charBuffer = stackalloc char[50];

        // Act
        bool byteSuccess = bigNumber.TryFormat(byteBuffer, out int bytesWritten);
        bool charSuccess = bigNumber.TryFormat(charBuffer, out int charsWritten);

        string byteResult = Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten).ToArray());
        string charResult = charBuffer.Slice(0, charsWritten).ToString();

        // Assert
        Assert.True(byteSuccess);
        Assert.True(charSuccess);
        Assert.Equal(charResult, byteResult);
        Assert.Equal(charsWritten, bytesWritten); // For ASCII characters, byte count should equal char count
    }
}