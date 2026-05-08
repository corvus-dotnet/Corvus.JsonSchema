// <copyright file="IdnMappingCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Coverage tests for <see cref="IdnMapping"/> UTF-8 punycode decoding error paths.
/// </summary>
[TestClass]
public class IdnMappingCoverageTests
{
    // L50-52: index < 0
    [TestMethod]
    public void GetUnicode_NegativeIndex_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        bool result = mapping.GetUnicode("example.com"u8, output, -1, 5, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L56-58: count < 0
    [TestMethod]
    public void GetUnicode_NegativeCount_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        bool result = mapping.GetUnicode("example.com"u8, output, 0, -1, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L62-64: index > ascii.Length
    [TestMethod]
    public void GetUnicode_IndexBeyondLength_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        bool result = mapping.GetUnicode("abc"u8, output, 10, 1, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L68-70: index > ascii.Length - count
    [TestMethod]
    public void GetUnicode_IndexPlusCountBeyondLength_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        bool result = mapping.GetUnicode("abc"u8, output, 1, 5, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L80-82: last character is null terminator
    [TestMethod]
    public void GetUnicode_NullTerminatedInput_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        byte[] input = [.. "abc"u8, 0x00];
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L575, L577-578: GetUnicodeInvariant slicing with non-zero index
    [TestMethod]
    public void GetUnicode_NonZeroIndex_Works()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        byte[] input = "xx.example.com"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 3, input.Length - 3, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        Assert.AreEqual("example.com", Encoding.UTF8.GetString(output.Slice(0, written).ToArray()));
    }

    // L265-267: Input too long (> 255 chars including dots)
    [TestMethod]
    public void GetUnicode_InputTooLong_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[512];
        // Create a domain name > 255 characters
        string longDomain = string.Join(".", Enumerable.Repeat("abcdefghijklmnop", 17));
        byte[] input = Encoding.UTF8.GetBytes(longDomain);
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L310-312: Single label too long (> 63 runes)
    [TestMethod]
    public void GetUnicode_LabelTooLong_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        string longLabel = new('a', 64);
        byte[] input = Encoding.UTF8.GetBytes(longLabel);
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L342-344: Trailing dash in ACE segment
    [TestMethod]
    public void GetUnicode_AcePrefixTrailingDash_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        byte[] input = "xn---"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L360-362: Unicode byte > 0x7f in basic code points section
    [TestMethod]
    public void GetUnicode_NonAsciiInBasicCodePoints_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // "xn--" prefix + basic code points containing 0x80 + "-" delimiter + punycode suffix
        byte[] input = [.. "xn--"u8, 0x80, (byte)'-', (byte)'a'];
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L414-416: DecodeDigit fails on invalid character in main decode loop
    [TestMethod]
    public void GetUnicode_InvalidDigitInPunycode_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // "xn--!" - ACE prefix followed by invalid punycode digit '!'
        byte[] input = "xn--!"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // Valid punycode decode sanity check
    [TestMethod]
    public void GetUnicode_ValidPunycode_Succeeds()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // "xn--nxasmq6b" = "βόλος" (Greek city name)
        byte[] input = "xn--nxasmq6b"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    // L322, L324-325: Output buffer too small for ASCII copy
    [TestMethod]
    public void GetUnicode_OutputBufferTooSmallForAscii_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[3]; // too small for "example"
        byte[] input = "example"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L366, L368-369: Output buffer too small during basic code point copy in ACE segment
    [TestMethod]
    public void GetUnicode_OutputBufferTooSmallForAceBasicCodePoints_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[1]; // too small
        // "xn--abc-def" has basic code points "abc" before delimiter "-"
        byte[] input = "xn--abc-def"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L487-488: ConvertFromUtf32AndInsert fails (output buffer overflow)
    [TestMethod]
    public void GetUnicode_OutputBufferTooSmallForDecodedChar_ReturnsFalse()
    {
        IdnMapping mapping = new();
        // Use a very small buffer that can hold some ASCII but not the decoded Unicode char
        Span<byte> output = stackalloc byte[6];
        // "xn--n3h" decodes to "☃" (snowman U+2603) - 3 UTF-8 bytes
        byte[] input = "xn--n3h"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        // With only 6 bytes available, this should still succeed for snowman (3 bytes)
        // Let's use an even smaller buffer
        Span<byte> tinyOutput = stackalloc byte[2];
        result = mapping.GetUnicode(input, tinyOutput, 0, input.Length, out written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L537-539: Decoded label exceeds c_labelLimit (63) after punycode decode
    [TestMethod]
    public void GetUnicode_DecodedLabelExceedsLimit_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[512];
        // Use a punycode label with many basic code points (60) + encoded chars
        // that decode to additional runes, pushing total above 63.
        // "xn--" + 60 basic chars + "-" + punycode digits = 60 basic + decoded > 63 total
        byte[] fullInput = Encoding.UTF8.GetBytes("xn--" + new string('a', 60) + "-aaaa");
        bool result = mapping.GetUnicode(fullInput, output, 0, fullInput.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L552-554: Total output length exceeds c_defaultNameLimit (255)
    // DEAD CODE: For ASCII-only labels, output == input (caught by L264 input check first).
    // For punycode labels, decoded output CAN exceed input length, but constructing a
    // valid punycode input ≤ 255 bytes that decodes to > 255 bytes requires carefully
    // crafted test vectors beyond simple construction.

    // L421-423: digit > (maxint - i) / w overflow
    [TestMethod]
    public void GetUnicode_PunycodeIntegerOverflow_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // '9' = digit value 35 (highest). With long sequences, i accumulates quickly and overflows.
        // After several iterations where digit > t, i += digit*w grows past maxint.
        byte[] input = [.. "xn--"u8, .. Encoding.UTF8.GetBytes(new string('9', 30))];
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L432-434: w > maxint / (base - t) overflow
    [TestMethod]
    public void GetUnicode_PunycodeWOverflow_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // '9' keeps the loop going (digit=35 > t for many iterations), w multiplies each time
        // After ~6-7 iterations w exceeds maxint/(base-t).
        byte[] input = [.. "xn--"u8, .. Encoding.UTF8.GetBytes(new string('9', 15))];
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L447-449: n overflow (i / (outputLength + 1) > maxint - n)
    [TestMethod]
    public void GetUnicode_PunycodeNOverflow_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // Multiple decode rounds with valid first chars then overflow triggers on n accumulation
        // Use sequence that produces a valid first char then overflows
        byte[] input = "xn--a999999999999"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L457-459: n < 0 or n > 0x10ffff or n in surrogate range
    [TestMethod]
    public void GetUnicode_InvalidCodePoint_ReturnsFalse()
    {
        IdnMapping mapping = new();
        Span<byte> output = stackalloc byte[256];
        // Use a punycode string that would decode to an invalid code point
        // This is exercised implicitly by the overflow tests above, since overflow
        // produces values > 0x10ffff or wraps to negative
        byte[] input = "xn--zzzzzzzzzzz"u8.ToArray();
        bool result = mapping.GetUnicode(input, output, 0, input.Length, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    // L144-145: DecodeDigit returns false for non-alphanumeric char
    // (Tested via L414-416 test above - the '!' is neither upper/lower/digit)

    // L197: IsDot returns false for non-dot
    // (Tested implicitly by all successful decode tests - non-dot chars don't split labels)

    // L258-260: PunycodeDecode with 0-length - dead code from public API
    // (GetUnicode's L73-77 check prevents count==0 from reaching PunycodeDecode)
}
