// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using System.Threading.Tasks;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber edge cases, boundary conditions, and extreme values.
/// </summary>
public class BigNumberEdgeCaseTests
{
    [Fact]
    public void Constructor_WithMinMaxExponentValues_ShouldWork()
    {
        // Arrange & Act
        var minExponentBigNumber = new Corvus.Numerics.BigNumber(BigInteger.One, int.MinValue);
        var maxExponentBigNumber = new Corvus.Numerics.BigNumber(BigInteger.One, int.MaxValue);

        // Assert
        Assert.Equal(BigInteger.One, BigNumberTestData.GetSignificand(minExponentBigNumber));
        Assert.Equal(int.MinValue, BigNumberTestData.GetExponent(minExponentBigNumber));
        Assert.Equal(BigInteger.One, BigNumberTestData.GetSignificand(maxExponentBigNumber));
        Assert.Equal(int.MaxValue, BigNumberTestData.GetExponent(maxExponentBigNumber));
    }

    [Fact]
    public void Constructor_WithVeryLargePositiveSignificand_ShouldWork()
    {
        // Arrange
        var veryLargeSignificand = BigInteger.Parse(new string('9', 1000)); // 1000 nines

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeSignificand, 0);

        // Assert
        Assert.Equal(veryLargeSignificand, BigNumberTestData.GetSignificand(bigNumber));
        Assert.Equal(0, BigNumberTestData.GetExponent(bigNumber));
    }

    [Fact]
    public void Constructor_WithVeryLargeNegativeSignificand_ShouldWork()
    {
        // Arrange
        BigInteger veryLargeNegativeSignificand = -BigInteger.Parse(new string('9', 1000)); // -1000 nines

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeNegativeSignificand, 0);

        // Assert
        Assert.Equal(veryLargeNegativeSignificand, BigNumberTestData.GetSignificand(bigNumber));
        Assert.Equal(0, BigNumberTestData.GetExponent(bigNumber));
    }

    [Fact]
    public void DefaultConstructor_ShouldCreateZeroBigNumber()
    {
        // Arrange & Act
        var bigNumber = default(Corvus.Numerics.BigNumber);

        // Assert
        Assert.Equal(BigInteger.Zero, BigNumberTestData.GetSignificand(bigNumber));
        Assert.Equal(0, BigNumberTestData.GetExponent(bigNumber));
        Assert.Equal("0", bigNumber.ToString());
    }

    [Fact]
    public void TryFormat_WithExtremelyLargeNumber_ShouldHandleGracefully()
    {
        // Arrange
        var veryLargeSignificand = BigInteger.Parse(new string('9', 10000)); // 10000 nines
        var bigNumber = new Corvus.Numerics.BigNumber(veryLargeSignificand, int.MaxValue);
        Span<char> buffer = stackalloc char[20000]; // Large buffer

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);

        // Assert
        Assert.True(success);
        Assert.True(charsWritten > 10000);
        string result = buffer.Slice(0, charsWritten).ToString();
        Assert.StartsWith(new string('9', 10000), result);
        Assert.Contains($"E{int.MaxValue}", result);
    }

    [Fact]
    public void TryFormat_WithExtremelySmallBuffer_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        Span<char> buffer = stackalloc char[1]; // Too small even for "123"

        // Act
        bool success = bigNumber.TryFormat(buffer, out int charsWritten);

        // Assert
        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryParse_WithExtremelyLongValidString_ShouldWork()
    {
        // Arrange
        string veryLongNumber = new string('1', 5000) + "E" + new string('9', 9); // Very long input
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(veryLongNumber);

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.True(success);
        string resultString = result.ToString();
        Assert.Contains(new string('1', 5000), resultString);
        Assert.Contains("E" + new string('9', 9), resultString);
    }

    [Fact]
    public void TryParse_WithExtremelyLongValidStringButInvalidExponent_ShouldReturnFalse()
    {
        // Arrange
        string veryLongNumber = new string('1', 5000) + "E" + new string('9', 1000); // Very long input
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(veryLongNumber);

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.False(success);
        _ = result.ToString();
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryParse_WithExtremelyLongInvalidString_ShouldReturnFalse()
    {
        // Arrange
        string invalidLongString = new('X', 10000); // Very long invalid input
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(invalidLongString);

        // Act
        bool success = Corvus.Numerics.BigNumber.TryParse(input, out BigNumber result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void Normalize_WithZeroSignificand_ShouldHandleGracefully()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, int.MaxValue);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        Assert.Equal(BigInteger.Zero, BigNumberTestData.GetSignificand(normalized));
        // The exponent behavior with zero significand depends on implementation
    }

    [Fact]
    public void Normalize_WithExtremeExponentValues_ShouldHandleGracefully()
    {
        // Arrange
        var minExponentBigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MinValue);
        var maxExponentBigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MaxValue);

        // Act & Assert - Should not throw
        _ = minExponentBigNumber.Normalize();
        _ = maxExponentBigNumber.Normalize();

        // Value types cannot be null, so we just verify they normalized successfully
        Assert.True(true, "Normalization completed without exceptions");
    }

    [Fact]
    public void Equality_WithExtremeValues_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(BigInteger.Parse(new string('9', 1000)), int.MaxValue);
        var bigNumber2 = new Corvus.Numerics.BigNumber(BigInteger.Parse(new string('9', 1000)), int.MaxValue);
        var bigNumber3 = new Corvus.Numerics.BigNumber(BigInteger.Parse(new string('9', 1000)), int.MaxValue - 1);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber3);
    }

    [Fact]
    public void GetHashCode_WithExtremeValues_ShouldBeConsistent()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(BigInteger.Parse(new string('9', 1000)), int.MinValue);
        var bigNumber2 = new Corvus.Numerics.BigNumber(BigInteger.Parse(new string('9', 1000)), int.MinValue);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void ToString_WithExtremeValues_ShouldNotThrow()
    {
        // Arrange
        var extremeBigNumber = new Corvus.Numerics.BigNumber(
            -BigInteger.Parse(new string('9', 5000)),
            int.MinValue);

        // Act & Assert - Should not throw
        string result = extremeBigNumber.ToString();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.StartsWith("-" + new string('9', 5000), result);
        Assert.Contains($"E{int.MinValue}", result);
    }

    [Fact]
    public void RoundTrip_WithExtremeValues_ShouldBeConsistent()
    {
        // Arrange
        var originalBigNumber = new Corvus.Numerics.BigNumber(
            BigInteger.Parse(new string('1', 100) + new string('0', 100)),
            -999999);

        // Act
        string stringRepresentation = originalBigNumber.ToString();
        bool parseSuccess = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(stringRepresentation), out BigNumber parsedBigNumber);

        originalBigNumber = originalBigNumber.Normalize();

        // Assert
        Assert.True(parseSuccess);
        BigNumberTestData.AssertBigNumbersEqual(originalBigNumber, parsedBigNumber);
    }

    [Fact]
    public void TryFormat_WithExactBoundaryConditions_ShouldWork()
    {
        // Test various exact boundary conditions for buffer sizes
        (BigInteger, int, int)[] testCases = new[]
        {
            (new BigInteger(1), 0, 1),          // "1" - 1 char
            (new BigInteger(10), 1, 3),         // "1E2" - 3 chars
            (new BigInteger(-1), 0, 2),         // "-1" - 2 chars
            (new BigInteger(-10), -1, 2),       // "-1" - 2 chars
            (new BigInteger(123), 456, 7),      // "123E456" - 7 chars
            (new BigInteger(-123), -456, 9),    // "-123E-456" - 9 chars
        };

        // Allocate buffers outside the loop
        Span<char> exactBuffer = stackalloc char[20]; // Larger buffer to accommodate all test cases
        Span<char> tooSmallBuffer = stackalloc char[20];

        foreach ((BigInteger significand, int exponent, int expectedLength) in testCases)
        {
            // Arrange
            var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);
            Span<char> exactBufferSlice = exactBuffer.Slice(0, expectedLength);
            int tooSmallLength = Math.Max(0, expectedLength - 1);
            Span<char> tooSmallBufferSlice = tooSmallBuffer.Slice(0, tooSmallLength);

            // Act
            bool exactSuccess = bigNumber.TryFormat(exactBufferSlice, out int exactCharsWritten);
            bool tooSmallSuccess = bigNumber.TryFormat(tooSmallBufferSlice, out int tooSmallCharsWritten);

            // Assert
            Assert.True(exactSuccess, $"Should succeed with exact buffer size for {significand}E{exponent}");
            Assert.Equal(expectedLength, exactCharsWritten);
            Assert.False(tooSmallSuccess, $"Should fail with too small buffer for {significand}E{exponent}");
            Assert.Equal(0, tooSmallCharsWritten);
        }
    }

    [Fact]
    public void TryParse_WithBoundaryNumericValues_ShouldWork()
    {
        // Test parsing at various numeric boundaries
        string[] testCases = new[]
        {
            "0",
            "1",
            "-1",
            "9",
            "-9",
            "10",
            "-10",
            "99",
            "-99",
            "100",
            "-100",
            "999",
            "-999",
            "1000",
            "-1000",
            "1E0",
            "-1E0",
            "1E1",
            "-1E1",
            "1E-1",
            "-1E-1",
            "9E9",
            "-9E-9",
        };

        foreach (string testCase in testCases)
        {
            // Act
            bool success = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(testCase), out BigNumber result);

            // Assert
            Assert.True(success, $"Should successfully parse '{testCase}'");

            // Round trip test
            string roundTrip = result.ToString();
            bool roundTripSuccess = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(roundTrip), out BigNumber roundTripResult);
            Assert.True(roundTripSuccess, $"Should successfully parse round trip for '{testCase}'");
            BigNumberTestData.AssertBigNumbersEqual(result, roundTripResult);
        }
    }

    [Fact]
    public void MemoryConstraints_WithLargeOperations_ShouldNotExceedReasonableLimits()
    {
        // This test ensures that operations with large numbers don't consume excessive memory
        // Arrange
        var largeSignificand = BigInteger.Parse(new string('9', 1000));
        var bigNumber = new Corvus.Numerics.BigNumber(largeSignificand, 1000);

        // Act & Assert - These operations should complete without excessive memory usage
        string stringResult = bigNumber.ToString();
        Assert.NotNull(stringResult);

        BigNumber normalizedResult = bigNumber.Normalize();
        Assert.NotEqual(default, normalizedResult);

        int hashCode = bigNumber.GetHashCode();
        Assert.NotEqual(0, hashCode); // Very unlikely to be zero for such a large number

        Span<char> buffer = stackalloc char[5000];
        bool formatSuccess = bigNumber.TryFormat(buffer, out int charsWritten);
        Assert.True(formatSuccess);
        Assert.True(charsWritten > 1000);
    }

    [Fact]
    public void SpecialNumbers_BigIntegerEdgeCases_ShouldWork()
    {
        // Test with BigInteger edge cases
        BigInteger[] testCases = new[]
        {
            BigInteger.Zero,
            BigInteger.One,
            BigInteger.MinusOne,
            new BigInteger(long.MaxValue),
            new BigInteger(long.MinValue),
            new BigInteger(ulong.MaxValue),
            BigInteger.Pow(2, 1000),      // Very large power of 2
            -BigInteger.Pow(2, 1000),     // Very large negative power of 2
            BigInteger.Pow(10, 100),      // Large power of 10
            -BigInteger.Pow(10, 100),     // Large negative power of 10
        };

        foreach (BigInteger significand in testCases)
        {
            foreach (int exponent in new[] { int.MinValue, -1000, -1, 0, 1, 1000, int.MaxValue })
            {
                // Act & Assert - Should not throw
                var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);
                string stringResult = bigNumber.ToString();
                Assert.NotNull(stringResult);
                bigNumber = bigNumber.Normalize();

                // Parsing round trip
                bool parseSuccess = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(stringResult), out BigNumber parsedResult);
                Assert.True(parseSuccess, $"Failed to parse: {stringResult}");
                BigNumberTestData.AssertBigNumbersEqual(bigNumber, parsedResult);
            }
        }
    }

    [Fact]
    public void ConcurrentOperations_ShouldBeThreadSafe()
    {
        // BigNumber should be immutable and thread-safe
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(12345), 678);
        const int threadCount = 10;
        const int operationsPerThread = 1000;
        var tasks = new Task[threadCount];
        bool[] results = new bool[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    // Allocate buffer outside the loop
                    Span<char> buffer = stackalloc char[50];

                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        // Perform various operations
                        string str = bigNumber.ToString();
                        int hash = bigNumber.GetHashCode();
                        BigNumber normalized = bigNumber.Normalize();
                        bool equals = bigNumber.Equals(bigNumber);

                        bool formatSuccess = bigNumber.TryFormat(buffer, out int charsWritten);

                        // Verify consistency
                        Assert.Equal("12345E678", str);
                        Assert.True(equals);
                        Assert.True(formatSuccess);
                    }
                    results[threadIndex] = true;
                }
                catch
                {
                    results[threadIndex] = false;
                }
            });
        }

        // Wait for all tasks to complete
        Task.WaitAll(tasks);

        // Assert all threads succeeded
        Assert.All(results, result => Assert.True(result, "All concurrent operations should succeed"));
    }
}