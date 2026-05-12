// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber.TryFormat(Span&lt;char&gt;) method.
/// </summary>
[TestClass]
public class BigNumberTryFormatCharTests
{
    [TestMethod]
    public void TryFormat_ToCharSpan_WithZeroSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        Span<char> buffer = stackalloc char[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "0");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithPositiveSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        Span<char> buffer = stackalloc char[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithNegativeSignificandZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 0);
        Span<char> buffer = stackalloc char[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "-456");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithZeroExponentPositiveSignificand_ShouldNotIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(789), 0);
        Span<char> buffer = stackalloc char[10];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "789");
        Assert.DoesNotContain("E", result);
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithPositiveExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E5");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithNegativeExponent_ShouldIncludeExponent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E-5");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithVeryLargeSignificand_ShouldFormatCorrectly()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
        var bigNumber = new Corvus.Numerics.BigNumber(largeSignificand, 0);
        Span<char> buffer = stackalloc char[100];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result,
            "1234567890123456789012345678901234567890123456789E1");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithVeryLargeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 999999);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E999999");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithVeryLargeNegativeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -999999);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E-999999");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithInsufficientBufferSize_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<char> buffer = stackalloc char[4]; // Too small for "123E5"

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);

        // Assert
        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithExactBufferSize_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<char> buffer = stackalloc char[5]; // Exact size for "123E5"
        buffer.Clear(); // Initialize to ensure we only write what we expect

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E5");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithExcessiveBufferSize_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        Span<char> buffer = stackalloc char[100]; // Much larger than needed

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "123E5");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithZeroLengthSpan_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        Span<char> buffer = Span<char>.Empty;

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);

        // Assert
        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DynamicData(nameof(BigNumberTestData.FormatData), typeof(BigNumberTestData))]
    public void TryFormat_ToCharSpan_WithVariousInputs_ShouldFormatCorrectly(
        BigInteger significand, int exponent, string expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);
        Span<char> buffer = stackalloc char[200]; // Large enough for any test case

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, expected,
            $"BigNumber({significand}, {exponent})");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithNegativeSignificandAndPositiveExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), 10);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "-789E10");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithNegativeSignificandAndNegativeExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), -10);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "-789E-10");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_WithZeroSignificandAndNonZeroExponent_ShouldFormatCorrectly()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 42);
        Span<char> buffer = stackalloc char[20];

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        BigNumberTestData.AssertFormatResult(success, charsWritten, result, "0");
    }

    [TestMethod]
    public void TryFormat_ToCharSpan_SignificandRequiringMaxFormatLength_ShouldWork()
    {
        // Arrange - Create a very large significand to test buffer limits
        var veryLargeSignificand = BigInteger.Parse(new string('9', 100)); // 100 nines
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeSignificand, 999);
        Span<char> buffer = stackalloc char[200]; // Large buffer

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);
        string result = buffer.Slice(0, charsWritten).ToString();

        // Assert
        Assert.IsTrue(success, "Should successfully format very large number");
        Assert.IsTrue(charsWritten > 100, "Should write more than 100 characters");
        Assert.StartsWith(new string('9', 100), result);
        StringAssert.Contains(result, "E999");
    }
}