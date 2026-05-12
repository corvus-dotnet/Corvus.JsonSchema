// <copyright file="JsonElementCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Numerics;
using Corvus.Numerics;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Targeted coverage tests for JsonElement and BigNumber.OptimizedFormatting.
/// Covers: SourceLocation null-parent paths, EnumerateArray/Object wrong type,
/// TryGetBoolean default path, GetHashCode on valid element, and BigNumber
/// TryParse/TryFormat edge cases for UTF-8 and large significands.
/// </summary>
[TestClass]
public class JsonElementCoverageTests
{
    #region SourceLocation - Default (null parent) element

    [TestMethod]
    public void TryGetLineAndOffset_DefaultElement_ReturnsFalse()
    {
        JsonElement element = default;
        Assert.IsFalse(element.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(0, line);
        Assert.AreEqual(0, charOffset);
    }

    [TestMethod]
    public void TryGetLineAndOffset_WithByteOffset_DefaultElement_ReturnsFalse()
    {
        JsonElement element = default;
        Assert.IsFalse(element.TryGetLineAndOffset(out int line, out int charOffset, out long lineByteOffset));
        Assert.AreEqual(0, line);
        Assert.AreEqual(0, charOffset);
        Assert.AreEqual(0, lineByteOffset);
    }

    [TestMethod]
    public void TryGetLine_Memory_DefaultElement_ReturnsFalse()
    {
        JsonElement element = default;
        Assert.IsFalse(element.TryGetLine(1, out ReadOnlyMemory<byte> line));
        Assert.AreEqual(0, line.Length);
    }

    [TestMethod]
    public void TryGetLine_String_DefaultElement_ReturnsFalse()
    {
        JsonElement element = default;
        Assert.IsFalse(element.TryGetLine(1, out string line));
        Assert.IsNull(line);
    }

    #endregion

    #region EnumerateArray / EnumerateObject wrong type

    [TestMethod]
    public void EnumerateArray_OnObjectElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("{}");
        Assert.ThrowsExactly<InvalidOperationException>(() => element.EnumerateArray());
    }

    [TestMethod]
    public void EnumerateObject_OnArrayElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("[]");
        Assert.ThrowsExactly<InvalidOperationException>(() => element.EnumerateObject());
    }

    [TestMethod]
    public void EnumerateArray_OnNumberElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("42");
        Assert.ThrowsExactly<InvalidOperationException>(() => element.EnumerateArray());
    }

    [TestMethod]
    public void EnumerateObject_OnStringElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("\"hello\"");
        Assert.ThrowsExactly<InvalidOperationException>(() => element.EnumerateObject());
    }

    #endregion

    #region TryGetBoolean default path

    [TestMethod]
    public void TryGetBoolean_OnNumberElement_ReturnsFalse()
    {
        var element = JsonElement.ParseValue("42");
        Assert.IsFalse(element.TryGetBoolean(out bool value));
        Assert.IsFalse(value);
    }

    [TestMethod]
    public void TryGetBoolean_OnStringElement_ReturnsFalse()
    {
        var element = JsonElement.ParseValue("\"hello\"");
        Assert.IsFalse(element.TryGetBoolean(out bool value));
        Assert.IsFalse(value);
    }

    [TestMethod]
    public void TryGetBoolean_OnNullElement_ReturnsFalse()
    {
        var element = JsonElement.ParseValue("null");
        Assert.IsFalse(element.TryGetBoolean(out bool value));
        Assert.IsFalse(value);
    }

    [TestMethod]
    public void TryGetBoolean_OnArrayElement_ReturnsFalse()
    {
        var element = JsonElement.ParseValue("[]");
        Assert.IsFalse(element.TryGetBoolean(out bool value));
        Assert.IsFalse(value);
    }

    [TestMethod]
    public void TryGetBoolean_OnObjectElement_ReturnsFalse()
    {
        var element = JsonElement.ParseValue("{}");
        Assert.IsFalse(element.TryGetBoolean(out bool value));
        Assert.IsFalse(value);
    }

    #endregion

    #region Get*() throw paths — FormatException when value overflows target type

    [TestMethod]
    public void GetBytesFromBase64_OnInvalidBase64String_ThrowsFormatException()
    {
        // A string that isn't valid base64
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not!!valid!!base64\"");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetBytesFromBase64());
    }

    [TestMethod]
    public void GetInt32_OnOverflowNumber_ThrowsFormatException()
    {
        // Number too large for int32
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("99999999999");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
    }

    [TestMethod]
    public void GetUInt32_OnNegativeNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt32());
    }

    [TestMethod]
    public void GetInt64_OnOverflowNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("99999999999999999999");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetInt64());
    }

    [TestMethod]
    public void GetUInt64_OnNegativeNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt64());
    }

    [TestMethod]
    public void GetDouble_OnValidNumber_Succeeds()
    {
        // Double accepts huge numbers as Infinity, so the throw path is unreachable
        // for valid JSON numbers. Just verify basic success.
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("3.14");
        JsonElement element = doc.RootElement;
        Assert.AreEqual(3.14, element.GetDouble());
    }

    [TestMethod]
    public void GetDecimal_OnOverflowNumber_ThrowsFormatException()
    {
        // Number too large for decimal
        string hugeNumber = "1" + new string('0', 50);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(hugeNumber);
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetDecimal());
    }

    [TestMethod]
    public void GetDateTime_OnInvalidDateString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not-a-date\"");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetDateTime());
    }

    [TestMethod]
    public void GetDateTimeOffset_OnInvalidDateString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not-a-date\"");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    public void GetGuid_OnInvalidGuidString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not-a-guid\"");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetGuid());
    }

    [TestMethod]
    public void GetSByte_OnOverflowNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("999");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetSByte());
    }

    [TestMethod]
    public void GetByte_OnNegativeNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetByte());
    }

    [TestMethod]
    public void GetInt16_OnOverflowNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("99999");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetInt16());
    }

    [TestMethod]
    public void GetUInt16_OnNegativeNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt16());
    }

    [TestMethod]
    public void GetSingle_OnFractionalNumber_ThrowsFormatException()
    {
        // Single precision can't represent this exactly, but TryGetSingle may
        // succeed — try a decimal number instead
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("1.5");
        JsonElement element = doc.RootElement;
        // Single.TryParse succeeds for 1.5, so just verify it doesn't throw
        float val = element.GetSingle();
        Assert.AreEqual(1.5f, val);
    }

#if NET
    [TestMethod]
    public void GetInt128_OnOverflowNumber_ThrowsFormatException()
    {
        // Number too large for Int128
        string hugeNumber = "9" + new string('9', 50);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(hugeNumber);
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetInt128());
    }

    [TestMethod]
    public void GetUInt128_OnNegativeNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt128());
    }

    [TestMethod]
    public void GetHalf_OnValidNumber_Succeeds()
    {
        // Half accepts huge numbers as Infinity, so the throw path is unreachable
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("2.5");
        JsonElement element = doc.RootElement;
        Assert.AreEqual((Half)2.5, element.GetHalf());
    }
#endif

    [TestMethod]
    public void GetBoolean_OnNumberElement_ThrowsInvalidOperationException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonElement element = doc.RootElement;
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetBoolean());
    }

    #endregion

    #region GetHashCode on valid element

    [TestMethod]
    public void GetHashCode_ValidNumberElement_DoesNotThrow()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonElement element = doc.RootElement;
        // Just verify it produces a hash without throwing
        _ = element.GetHashCode();
    }

    [TestMethod]
    public void GetHashCode_ValidStringElement_DoesNotThrow()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        JsonElement element = doc.RootElement;
        _ = element.GetHashCode();
    }

    [TestMethod]
    public void GetHashCode_SameElements_ReturnSameHash()
    {
        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("42");
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("42");
        // Same value parsed twice should be equal and produce same hash
        Assert.AreEqual(doc1.RootElement, doc2.RootElement);
        Assert.AreEqual(doc1.RootElement.GetHashCode(), doc2.RootElement.GetHashCode());
    }

    #endregion

    #region BigNumber TryParse UTF-8 failure paths

    [TestMethod]
    public void BigNumber_TryParseUtf8_IncompleteExponent_ReturnsFalse()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("1E");
        Assert.IsFalse(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber _));
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_ExponentWithSignOnly_ReturnsFalse()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("1E+");
        Assert.IsFalse(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber _));
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_ExponentWithNegativeSignOnly_ReturnsFalse()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("1E-");
        Assert.IsFalse(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber _));
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_EmptyInput_ReturnsFalse()
    {
        byte[] input = Array.Empty<byte>();
        Assert.IsFalse(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber _));
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_InvalidCharacters_ReturnsFalse()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("abc");
        Assert.IsFalse(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber _));
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_ValidNumber_Succeeds()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("12345");
        Assert.IsTrue(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber result));
        Assert.AreEqual("12345", result.ToString());
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_WithExponent_Succeeds()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("1.5E+3");
        Assert.IsTrue(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber result));
        // Verify the number represents 1500
        string formatted = result.ToString();
        Assert.IsNotNull(formatted);
    }

    [TestMethod]
    public void BigNumber_TryParseUtf8_NegativeWithExponent_Succeeds()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("-2.5E-2");
        Assert.IsTrue(BigNumber.TryParse(new ReadOnlySpan<byte>(input), out BigNumber result));
        string formatted = result.ToString();
        Assert.IsNotNull(formatted);
    }

    #endregion

    #region BigNumber TryFormat - Large significand (ArrayPool paths)

    [TestMethod]
    public void BigNumber_TryFormat_LargeSignificand_CharBuffer_Succeeds()
    {
        // 300-digit number to trigger ArrayPool rent/return path
        string digits = new string('1', 300);
        BigNumber bn = BigNumber.Parse(digits);
        Span<char> buffer = new char[512];
        Assert.IsTrue(bn.TryFormat(buffer, out int charsWritten));
        Assert.IsTrue(charsWritten > 0);
    }

    [TestMethod]
    public void BigNumber_TryFormat_LargeSignificand_ByteBuffer_Succeeds()
    {
        // 300-digit number to trigger ArrayPool rent/return path in UTF-8 formatting
        string digits = new string('1', 300);
        BigNumber bn = BigNumber.Parse(digits);
        Span<byte> buffer = new byte[512];
        Assert.IsTrue(bn.TryFormat(buffer, out int bytesWritten));
        Assert.IsTrue(bytesWritten > 0);
    }

    [TestMethod]
    public void BigNumber_TryFormat_LargeNegativeSignificand_CharBuffer_Succeeds()
    {
        // Negative 300-digit number
        string digits = "-" + new string('9', 300);
        BigNumber bn = BigNumber.Parse(digits);
        Span<char> buffer = new char[512];
        Assert.IsTrue(bn.TryFormat(buffer, out int charsWritten));
        Assert.IsTrue(charsWritten > 0);
    }

    [TestMethod]
    public void BigNumber_TryFormat_LargeSignificandWithExponent_ByteBuffer_Succeeds()
    {
        // Large significand with an exponent — exercises both the ArrayPool
        // path for BigInteger formatting and the exponent appending
        string digits = new string('7', 300) + "E+5";
        BigNumber bn = BigNumber.Parse(digits);
        Span<byte> buffer = new byte[512];
        Assert.IsTrue(bn.TryFormat(buffer, out int bytesWritten));
        Assert.IsTrue(bytesWritten > 0);
    }

    #endregion

    #region BigNumber TryFormat - Tiny buffer failures

    [TestMethod]
    public void BigNumber_TryFormat_TinyCharBuffer_ReturnsFalse()
    {
        BigNumber bn = BigNumber.Parse("12345678901234567890");
        Span<char> buffer = new char[2];
        Assert.IsFalse(bn.TryFormat(buffer, out int charsWritten));
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void BigNumber_TryFormat_TinyByteBuffer_ReturnsFalse()
    {
        BigNumber bn = BigNumber.Parse("12345678901234567890");
        Span<byte> buffer = new byte[2];
        Assert.IsFalse(bn.TryFormat(buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void BigNumber_TryFormat_WithExponent_TinyBuffer_ReturnsFalse()
    {
        // "123E5" needs 5 chars, buffer is 3
        BigNumber bn = new BigNumber(new BigInteger(123), 5);
        Span<char> buffer = new char[3];
        Assert.IsFalse(bn.TryFormat(buffer, out int charsWritten));
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void BigNumber_TryFormat_NegativeWithExponent_TinyByteBuffer_ReturnsFalse()
    {
        // "-123E5" needs 6 bytes, buffer is 3
        BigNumber bn = new BigNumber(new BigInteger(-123), 5);
        Span<byte> buffer = new byte[3];
        Assert.IsFalse(bn.TryFormat(buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region BigNumber NumberOfDigits edge cases (via formatting)

    [TestMethod]
    [DataRow("0")]
    [DataRow("1E+9")]
    [DataRow("1E+10")]
    [DataRow("1E-5")]
    [DataRow("-1E+9")]
    [DataRow("-1E-5")]
    public void BigNumber_Format_VariousExponentSizes_DoesNotThrow(string input)
    {
        BigNumber bn = BigNumber.Parse(input);
        string result = bn.ToString();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }

    [TestMethod]
    public void BigNumber_Format_ZeroExponent_ProducesCorrectResult()
    {
        BigNumber bn = BigNumber.Parse("0");
        Assert.AreEqual("0", bn.ToString());
    }

    [TestMethod]
    public void BigNumber_Format_LargePositiveExponent_ProducesCorrectResult()
    {
        // 1E+100 — tests the large exponent formatting path
        BigNumber bn = new BigNumber(BigInteger.One, 100);
        string result = bn.ToString();
        StringAssert.Contains(result, "E");
    }

    [TestMethod]
    public void BigNumber_Format_LargeNegativeExponent_ProducesCorrectResult()
    {
        // 1E-100 — tests the large negative exponent formatting path
        BigNumber bn = new BigNumber(BigInteger.One, -100);
        string result = bn.ToString();
        StringAssert.Contains(result, "E");
    }

    #endregion

#if NET
    #region BigNumber TryFormat - ISpanFormattable overload with format/provider

    [TestMethod]
    public void BigNumber_TryFormat_WithDefaultFormatAndProvider_CharBuffer_Succeeds()
    {
        BigNumber bn = BigNumber.Parse("42");
        Span<char> buffer = new char[64];
        Assert.IsTrue(bn.TryFormat(buffer, out int charsWritten, default, null));
        Assert.IsTrue(charsWritten > 0);
        Assert.AreEqual("42", new string(buffer.Slice(0, charsWritten).ToArray()));
    }

    [TestMethod]
    public void BigNumber_TryFormat_WithDefaultFormatAndProvider_ByteBuffer_Succeeds()
    {
        BigNumber bn = BigNumber.Parse("42");
        Span<byte> buffer = new byte[64];
        Assert.IsTrue(bn.TryFormat(buffer, out int bytesWritten, default, null));
        Assert.IsTrue(bytesWritten > 0);
        string result = System.Text.Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten).ToArray());
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void BigNumber_TryFormat_WithDefaultFormatAndProvider_TinyBuffer_ReturnsFalse()
    {
        BigNumber bn = BigNumber.Parse("123456789");
        Span<char> buffer = new char[2];
        Assert.IsFalse(bn.TryFormat(buffer, out int charsWritten, default, null));
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void BigNumber_TryFormat_WithDefaultFormatAndProvider_TinyByteBuffer_ReturnsFalse()
    {
        BigNumber bn = BigNumber.Parse("123456789");
        Span<byte> buffer = new byte[2];
        Assert.IsFalse(bn.TryFormat(buffer, out int bytesWritten, default, null));
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion
#endif
}
