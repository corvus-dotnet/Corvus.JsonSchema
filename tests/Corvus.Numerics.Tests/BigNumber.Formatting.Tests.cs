// <copyright file="BigNumber.Formatting.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberFormattingTests
{
    #region UTF-16 ToString Tests

    [Fact]
    public void ToString_IntegerNoExponent_ExactOutput()
    {
        BigNumber value = new(12345, 0);

        string result = value.ToString();

        result.ShouldBe("12345");
        result.Length.ShouldBe(5);
    }

    [Fact]
    public void ToString_PositiveExponent_ExactOutput()
    {
        BigNumber value = new(123, 5);

        string result = value.ToString();

        result.ShouldBe("123E5");
        result.Length.ShouldBe(5);
        // Verify exact characters
        result[0].ShouldBe('1');
        result[1].ShouldBe('2');
        result[2].ShouldBe('3');
        result[3].ShouldBe('E');
        result[4].ShouldBe('5');
    }

    [Fact]
    public void ToString_NegativeExponent_ExactOutput()
    {
        BigNumber value = new(456, -10);

        string result = value.ToString();

        result.ShouldBe("456E-10");
        result.Length.ShouldBe(7);
        result[0].ShouldBe('4');
        result[1].ShouldBe('5');
        result[2].ShouldBe('6');
        result[3].ShouldBe('E');
        result[4].ShouldBe('-');
        result[5].ShouldBe('1');
        result[6].ShouldBe('0');
    }

    [Fact]
    public void ToString_NegativeNumber_ExactOutput()
    {
        BigNumber value = new(-789, 0);

        string result = value.ToString();

        result.ShouldBe("-789");
        result.Length.ShouldBe(4);
        result[0].ShouldBe('-');
        result[1].ShouldBe('7');
        result[2].ShouldBe('8');
        result[3].ShouldBe('9');
    }

    [Fact]
    public void ToString_NegativeWithExponent_ExactOutput()
    {
        BigNumber value = new(-123, 7);

        string result = value.ToString();

        result.ShouldBe("-123E7");
        result.Length.ShouldBe(6);
        result[0].ShouldBe('-');
        result[1].ShouldBe('1');
        result[2].ShouldBe('2');
        result[3].ShouldBe('3');
        result[4].ShouldBe('E');
        result[5].ShouldBe('7');
    }

    [Fact]
    public void ToString_Zero_ExactOutput()
    {
        BigNumber value = BigNumber.Zero;

        string result = value.ToString();

        result.ShouldBe("0");
        result.Length.ShouldBe(1);
        result[0].ShouldBe('0');
    }

    [Fact]
    public void ToString_One_ExactOutput()
    {
        BigNumber value = BigNumber.One;

        string result = value.ToString();

        result.ShouldBe("1");
        result.Length.ShouldBe(1);
        result[0].ShouldBe('1');
    }

    [Fact]
    public void ToString_MinusOne_ExactOutput()
    {
        BigNumber value = BigNumber.MinusOne;

        string result = value.ToString();

        result.ShouldBe("-1");
        result.Length.ShouldBe(2);
        result[0].ShouldBe('-');
        result[1].ShouldBe('1');
    }

    [Fact]
    public void ToString_LargeExponent_ExactOutput()
    {
        BigNumber value = new(999, 100);

        string result = value.ToString();

        result.ShouldBe("999E100");
        result.Length.ShouldBe(7);
    }

    [Fact]
    public void ToString_SmallExponent_ExactOutput()
    {
        BigNumber value = new(5, -50);

        string result = value.ToString();

        result.ShouldBe("5E-50");
        result.Length.ShouldBe(5);
    }

    #endregion

    #region UTF-16 TryFormat Tests

    [Fact]
    public void TryFormat_UTF16_IntegerNoExponent_ExactChars()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);

        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12345");

        // Verify exact characters
        buffer[0].ShouldBe('1');
        buffer[1].ShouldBe('2');
        buffer[2].ShouldBe('3');
        buffer[3].ShouldBe('4');
        buffer[4].ShouldBe('5');
    }

    [Fact]
    public void TryFormat_UTF16_PositiveExponent_ExactChars()
    {
        BigNumber value = new(789, 3);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);

        buffer[0].ShouldBe('7');
        buffer[1].ShouldBe('8');
        buffer[2].ShouldBe('9');
        buffer[3].ShouldBe('E');
        buffer[4].ShouldBe('3');
    }

    [Fact]
    public void TryFormat_UTF16_NegativeExponent_ExactChars()
    {
        BigNumber value = new(42, -8);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);

        buffer[0].ShouldBe('4');
        buffer[1].ShouldBe('2');
        buffer[2].ShouldBe('E');
        buffer[3].ShouldBe('-');
        buffer[4].ShouldBe('8');
    }

    [Fact]
    public void TryFormat_UTF16_NegativeNumber_ExactChars()
    {
        BigNumber value = new(-567, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(4);

        buffer[0].ShouldBe('-');
        buffer[1].ShouldBe('5');
        buffer[2].ShouldBe('6');
        buffer[3].ShouldBe('7');
    }

    [Fact]
    public void TryFormat_UTF16_Zero_ExactChars()
    {
        BigNumber value = BigNumber.Zero;
        Span<char> buffer = stackalloc char[16];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(1);
        buffer[0].ShouldBe('0');
    }

    [Fact]
    public void TryFormat_UTF16_LargeNumber_ExactChars()
    {
        BigNumber value = new(BigInteger.Parse("123456789012345678901234567890"), 0);
        Span<char> buffer = stackalloc char[256];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        // This gets normalized to "12345678901234567890123456789E1"
        charsWritten.ShouldBe(31);

        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12345678901234567890123456789E1");
    }

    [Fact]
    public void TryFormat_UTF16_MultiDigitExponent_ExactChars()
    {
        BigNumber value = new(7, 123);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);

        buffer[0].ShouldBe('7');
        buffer[1].ShouldBe('E');
        buffer[2].ShouldBe('1');
        buffer[3].ShouldBe('2');
        buffer[4].ShouldBe('3');
    }

    [Fact]
    public void TryFormat_UTF16_InsufficientBuffer_ReturnsFalse()
    {
        BigNumber value = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        Span<char> buffer = stackalloc char[5]; // Too small

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeFalse();
        charsWritten.ShouldBe(0);
    }

    #endregion

    #region UTF-8 TryFormat Tests

    [Fact]
    public void TryFormat_UTF8_IntegerNoExponent_ExactBytes()
    {
        BigNumber value = new(98765, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(5);

        // Verify exact bytes
        buffer[0].ShouldBe((byte)'9');
        buffer[1].ShouldBe((byte)'8');
        buffer[2].ShouldBe((byte)'7');
        buffer[3].ShouldBe((byte)'6');
        buffer[4].ShouldBe((byte)'5');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("98765");
    }

    [Fact]
    public void TryFormat_UTF8_PositiveExponent_ExactBytes()
    {
        BigNumber value = new(321, 15);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(6);

        buffer[0].ShouldBe((byte)'3');
        buffer[1].ShouldBe((byte)'2');
        buffer[2].ShouldBe((byte)'1');
        buffer[3].ShouldBe((byte)'E');
        buffer[4].ShouldBe((byte)'1');
        buffer[5].ShouldBe((byte)'5');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("321E15");
    }

    [Fact]
    public void TryFormat_UTF8_NegativeExponent_ExactBytes()
    {
        BigNumber value = new(654, -99);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(7);

        buffer[0].ShouldBe((byte)'6');
        buffer[1].ShouldBe((byte)'5');
        buffer[2].ShouldBe((byte)'4');
        buffer[3].ShouldBe((byte)'E');
        buffer[4].ShouldBe((byte)'-');
        buffer[5].ShouldBe((byte)'9');
        buffer[6].ShouldBe((byte)'9');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("654E-99");
    }

    [Fact]
    public void TryFormat_UTF8_NegativeNumber_ExactBytes()
    {
        BigNumber value = new(-888, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(4);

        buffer[0].ShouldBe((byte)'-');
        buffer[1].ShouldBe((byte)'8');
        buffer[2].ShouldBe((byte)'8');
        buffer[3].ShouldBe((byte)'8');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("-888");
    }

    [Fact]
    public void TryFormat_UTF8_Zero_ExactBytes()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> buffer = stackalloc byte[16];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(1);
        buffer[0].ShouldBe((byte)'0');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("0");
    }

    [Fact]
    public void TryFormat_UTF8_LargeSignificand_ExactBytes()
    {
        BigNumber value = new(BigInteger.Parse("987654321098765432109876543210"), 0);
        Span<byte> buffer = stackalloc byte[256];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        // This gets normalized to "98765432109876543210987654321E1"
        bytesWritten.ShouldBe(31);

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("98765432109876543210987654321E1");

        // Verify first and last bytes
        buffer[0].ShouldBe((byte)'9');
        buffer[30].ShouldBe((byte)'1');
    }

    [Fact]
    public void TryFormat_UTF8_NegativeWithExponent_ExactBytes()
    {
        BigNumber value = new(-246, 12);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(7);

        buffer[0].ShouldBe((byte)'-');
        buffer[1].ShouldBe((byte)'2');
        buffer[2].ShouldBe((byte)'4');
        buffer[3].ShouldBe((byte)'6');
        buffer[4].ShouldBe((byte)'E');
        buffer[5].ShouldBe((byte)'1');
        buffer[6].ShouldBe((byte)'2');

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("-246E12");
    }

    [Fact]
    public void TryFormat_UTF8_InsufficientBuffer_ReturnsFalse()
    {
        BigNumber value = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        Span<byte> buffer = stackalloc byte[5]; // Too small

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormat_UTF8_SingleDigit_ExactBytes()
    {
        BigNumber value = new(9, 0);
        Span<byte> buffer = stackalloc byte[16];

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(1);
        buffer[0].ShouldBe((byte)'9');
    }

    #endregion

    #region Format Consistency Tests

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_PositiveInteger()
    {
        BigNumber value = new(123456, 0);

        // UTF-16
        Span<char> charBuffer = stackalloc char[128];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        // UTF-8
        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        utf16.ShouldBe("123456");
        charsWritten.ShouldBe(bytesWritten); // ASCII chars = bytes
        charsWritten.ShouldBe(6);
    }

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_NegativeInteger()
    {
        BigNumber value = new(-987654, 0);

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        utf16.ShouldBe("-987654");
        charsWritten.ShouldBe(bytesWritten);
        charsWritten.ShouldBe(7);
    }

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_WithPositiveExponent()
    {
        BigNumber value = new(12345, 10);

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        utf16.ShouldBe("12345E10");
        charsWritten.ShouldBe(bytesWritten);
        charsWritten.ShouldBe(8);
    }

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_WithNegativeExponent()
    {
        BigNumber value = new(777, -25);

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        utf16.ShouldBe("777E-25");
        charsWritten.ShouldBe(bytesWritten);
        charsWritten.ShouldBe(7);
    }

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_Zero()
    {
        BigNumber value = BigNumber.Zero;

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        utf16.ShouldBe("0");
        charsWritten.ShouldBe(bytesWritten);
        charsWritten.ShouldBe(1);

        // Verify exact byte/char
        charBuffer[0].ShouldBe('0');
        byteBuffer[0].ShouldBe((byte)'0');
    }

    [Fact]
    public void Format_UTF16AndUTF8_ExactMatch_LargeSignificand()
    {
        BigNumber value = new(BigInteger.Parse("111222333444555666777888999000"), 0);

        Span<char> charBuffer = stackalloc char[256];
        value.TryFormat(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[256];
        value.TryFormat(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16.ShouldBe(utf8);
        // This gets normalized to "111222333444555666777888999E3"
        utf16.ShouldBe("111222333444555666777888999E3");
        charsWritten.ShouldBe(bytesWritten);
        charsWritten.ShouldBe(29);
    }

    #endregion

    #region Scientific Notation Exact Format Tests

    [Fact]
    public void Format_ScientificNotation_SingleDigitExponent_ExactFormat()
    {
        (BigNumber, string)[] testCases = new[]
        {
            (new BigNumber(1, 1), "1E1"),
            (new BigNumber(1, 5), "1E5"),
            (new BigNumber(1, 9), "1E9"),
            (new BigNumber(1, -1), "1E-1"),
            (new BigNumber(1, -5), "1E-5"),
            (new BigNumber(1, -9), "1E-9"),
        };

        foreach ((BigNumber value, string? expected) in testCases)
        {
            string result = value.ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expected.Length);
        }
    }

    [Fact]
    public void Format_ScientificNotation_DoubleDigitExponent_ExactFormat()
    {
        (BigNumber, string)[] testCases = new[]
        {
            (new BigNumber(99, 10), "99E10"),
            (new BigNumber(88, 50), "88E50"),
            (new BigNumber(77, 99), "77E99"),
            (new BigNumber(66, -10), "66E-10"),
            (new BigNumber(55, -50), "55E-50"),
            (new BigNumber(44, -99), "44E-99"),
        };

        foreach ((BigNumber value, string? expected) in testCases)
        {
            string result = value.ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expected.Length);
        }
    }

    [Fact]
    public void Format_ScientificNotation_TripleDigitExponent_ExactFormat()
    {
        (BigNumber, string)[] testCases = new[]
        {
            (new BigNumber(333, 100), "333E100"),
            (new BigNumber(222, 500), "222E500"),
            (new BigNumber(111, 999), "111E999"),
            (new BigNumber(444, -100), "444E-100"),
            (new BigNumber(555, -500), "555E-500"),
            (new BigNumber(666, -999), "666E-999"),
        };

        foreach ((BigNumber value, string? expected) in testCases)
        {
            string result = value.ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expected.Length);
        }
    }

    [Fact]
    public void Format_ScientificNotation_NoExponent_ExactFormat()
    {
        (BigNumber, string)[] testCases = new[]
        {
            (new BigNumber(0, 0), "0"),
            (new BigNumber(1, 0), "1"),
            (new BigNumber(9, 0), "9"),
            (new BigNumber(42, 0), "42"),
            (new BigNumber(789, 0), "789"),
            (new BigNumber(12345, 0), "12345"),
        };

        foreach ((BigNumber value, string? expected) in testCases)
        {
            string result = value.ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expected.Length);
            result.ShouldNotContain("E");
        }
    }

    #endregion

    #region Edge Cases with Exact Validation

    [Fact]
    public void Format_MaximumExponent_ExactOutput()
    {
        BigNumber value = new(123, int.MaxValue - 1000);

        string result = value.ToString();

        result.ShouldStartWith("123E");
        result.ShouldContain("2147482647"); // int.MaxValue - 1000
    }

    [Fact]
    public void Format_MinimumExponent_ExactOutput()
    {
        BigNumber value = new(456, int.MinValue + 1000);

        string result = value.ToString();

        result.ShouldStartWith("456E-");
        result.ShouldContain("2147482648"); // -(int.MinValue + 1000) as string
    }

    [Fact]
    public void Format_PowersOfTen_ExactFormats()
    {
        (BigNumber, string, int)[] testCases = new[]
        {
            (new BigNumber(1, 0), "1", 1),
            (new BigNumber(1, 1), "1E1", 3),
            (new BigNumber(1, 2), "1E2", 3),
            (new BigNumber(1, 3), "1E3", 3),
            (new BigNumber(1, -1), "1E-1", 4),
            (new BigNumber(1, -2), "1E-2", 4),
            (new BigNumber(1, -3), "1E-3", 4),
        };

        foreach ((BigNumber value, string? expected, int expectedLength) in testCases)
        {
            string result = value.ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expectedLength);
        }
    }

    [Fact]
    public void Format_SpecialValues_ExactFormats()
    {
        (BigNumber, string, int)[] testCases = new[]
        {
            (BigNumber.Zero, "0", 1),
            (BigNumber.One, "1", 1),
            (BigNumber.MinusOne, "-1", 2),
            (new BigNumber(10, 0), "1E1", 3),  // Normalized
            (new BigNumber(100, 0), "1E2", 3), // Normalized
            (new BigNumber(-10, 0), "-1E1", 4), // Normalized
        };

        foreach ((BigNumber value, string? expected, int expectedLength) in testCases)
        {
            string result = value.Normalize().ToString();
            result.ShouldBe(expected);
            result.Length.ShouldBe(expectedLength);
        }
    }

    [Fact]
    public void Format_ConsecutiveSmallIntegers_ExactFormats()
    {
        for (int i = 0; i <= 9; i++)
        {
            BigNumber value = new(i, 0);
            string result = value.ToString();

            result.ShouldBe(i.ToString());
            result.Length.ShouldBe(1);
            result[0].ShouldBe((char)('0' + i));
        }
    }

    [Fact]
    public void Format_NegativeSmallIntegers_ExactFormats()
    {
        for (int i = -9; i <= -1; i++)
        {
            BigNumber value = new(i, 0);
            string result = value.ToString();

            result.ShouldBe(i.ToString());
            result.Length.ShouldBe(2);
            result[0].ShouldBe('-');
            result[1].ShouldBe((char)('0' + Math.Abs(i)));
        }
    }

    #endregion

    #region Round-Trip with Exact Validation

    [Fact]
    public void RoundTrip_SimpleInteger_ExactMatch()
    {
        BigNumber original = new(12345, 0);
        string formatted = original.ToString();

        formatted.ShouldBe("12345");

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
        parsed.Significand.ShouldBe(original.Significand);
        parsed.Exponent.ShouldBe(original.Exponent);
    }

    [Fact]
    public void RoundTrip_WithPositiveExponent_ExactMatch()
    {
        BigNumber original = new(789, 15);
        string formatted = original.ToString();

        formatted.ShouldBe("789E15");

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_WithNegativeExponent_ExactMatch()
    {
        BigNumber original = new(456, -20);
        string formatted = original.ToString();

        formatted.ShouldBe("456E-20");

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_NegativeNumber_ExactMatch()
    {
        BigNumber original = new(-987, 5);
        string formatted = original.ToString();

        formatted.ShouldBe("-987E5");

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_Zero_ExactMatch()
    {
        BigNumber original = BigNumber.Zero;
        string formatted = original.ToString();

        formatted.ShouldBe("0");
        formatted.Length.ShouldBe(1);

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_UTF8_ExactMatch()
    {
        BigNumber original = new(654321, -8);

        Span<byte> buffer = stackalloc byte[128];
        original.TryFormat(buffer, out int bytesWritten, default, null);

        // "654321E-8" = 9 bytes
        bytesWritten.ShouldBe(9);

        ReadOnlySpan<byte> utf8 = buffer.Slice(0, bytesWritten);
        string formatted = StringFromSpan.CreateFromUtf8(utf8);
        formatted.ShouldBe("654321E-8");

        var parsed = BigNumber.Parse(utf8);
        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_LargeSignificand_ExactMatch()
    {
        var largeValue = BigInteger.Parse("123456789012345678901234567890");
        BigNumber original = new(largeValue, -10);

        string formatted = original.ToString();
        // This gets normalized to "12345678901234567890123456789E-9" (trailing 0 removed)
        formatted.ShouldBe("12345678901234567890123456789E-9");

        var parsed = BigNumber.Parse(formatted);
        parsed.ShouldBe(original);
        // After parsing and normalization, should be equivalent
        parsed.Normalize().Significand.ShouldBe(BigInteger.Parse("12345678901234567890123456789"));
        parsed.Normalize().Exponent.ShouldBe(-9);
    }

    #endregion

    #region Buffer Boundary Tests

    [Fact]
    public void TryFormat_UTF16_ExactSizeBuffer_Succeeds()
    {
        BigNumber value = new(123, 5);
        Span<char> buffer = stackalloc char[5]; // Exact size for "123E5"

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);

        string result = buffer.ToString();
        result.ShouldBe("123E5");
    }

    [Fact]
    public void TryFormat_UTF16_OneTooSmall_Fails()
    {
        BigNumber value = new(123, 5);
        Span<char> buffer = stackalloc char[4]; // One too small for "123E5"

        bool success = value.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeFalse();
        charsWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormat_UTF8_ExactSizeBuffer_Succeeds()
    {
        // This test validates that we can format to a reasonably-sized buffer
        BigNumber value = new(12, -1);

        // Use a buffer that's large enough
        Span<byte> buffer = stackalloc byte[10];
        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBeGreaterThan(0);
        bytesWritten.ShouldBeLessThanOrEqualTo(10);

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("12E-1");
    }

    [Fact]
    public void TryFormat_UTF8_OneTooSmall_Fails()
    {
        BigNumber value = new(777, -9);
        Span<byte> buffer = stackalloc byte[5]; // One too small for "777E-9"

        bool success = value.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    #endregion

    #region Culture and Format Provider Tests

    [Fact]
    public void Format_InvariantCulture_ExactOutput()
    {
        BigNumber value = new(123456, -3);

        string result = value.ToString(null, CultureInfo.InvariantCulture);

        // Invariant culture should produce same output as default
        result.ShouldBe("123456E-3");
    }

    [Fact]
    public void Format_NullFormatProvider_ExactOutput()
    {
        BigNumber value = new(98765, 7);

        string result = value.ToString(null, null);

        result.ShouldBe("98765E7");
    }

    #endregion
}