// <copyright file="Utf8JsonPointerToolsCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="Utf8JsonPointerTools"/> targeting uncovered validation
/// and decode error paths.
/// </summary>
[TestClass]
public class Utf8JsonPointerToolsCoverageTests
{
    // ─── L73-80: Invalid UTF-8 multi-byte sequence in pointer ─────────────

    [TestMethod]
    public void Validate_InvalidUtf8InSegment_ReturnsFalse()
    {
        // 0xFF is not a valid UTF-8 lead byte → Rune.DecodeFromUtf8 returns OperationStatus != Done
        byte[] pointer = [(byte)'/', 0xFF, 0xFF];
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    [TestMethod]
    public void Validate_TruncatedUtf8InSegment_ReturnsFalse()
    {
        // 0xC2 is a 2-byte lead byte but nothing follows → invalid
        byte[] pointer = [(byte)'/', 0xC2];
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    [TestMethod]
    public void Validate_OverlongUtf8Sequence_ReturnsFalse()
    {
        // Overlong 2-byte encoding of U+002F (/) — 0xC0 0xAF
        byte[] pointer = [(byte)'/', 0xC0, 0xAF];
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    // ─── L85-87: Invalid byte value in reference-token ─────────────────────

    [TestMethod]
    public void Validate_Byte0x7E_ReturnsFalse()
    {
        // 0x7E is '~' which must be followed by 0 or 1 (escape sequence).
        // But here '~' at end of segment is invalid.
        byte[] pointer = [(byte)'/', (byte)'a', (byte)'~'];
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    [TestMethod]
    public void Validate_TildeFollowedByInvalidDigit_ReturnsFalse()
    {
        // ~2 is invalid in RFC 6901
        byte[] pointer = [(byte)'/', (byte)'a', (byte)'~', (byte)'2'];
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    // ─── L127-137: Relative pointer with index manipulation ──────────────

    [TestMethod]
    public void ValidateRelative_WithPlusIndexManipulation_IsValid()
    {
        // "1+2/foo" — origin=1, index manipulation +2, then json-pointer /foo
        byte[] pointer = "1+2/foo"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_WithMinusIndexManipulation_IsValid()
    {
        // "2-1/bar" — origin=2, index manipulation -1, then json-pointer /bar
        byte[] pointer = "2-1/bar"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_WithMultiDigitIndex_IsValid()
    {
        // "3+12" — origin=3, index manipulation +12, no pointer after (valid: ends after index)
        byte[] pointer = "3+12"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_WithMinusAndPointer_IsValid()
    {
        // "10-3/baz/0" — multi-digit origin, minus manipulation, then pointer
        byte[] pointer = "10-3/baz/0"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_WithHashTerminator_IsValid()
    {
        // "2+1#" — index manipulation followed by '#'
        byte[] pointer = "2+1#"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_InvalidLeadingZeroAfterPlus_ReturnsFalse()
    {
        // "1+0" — after +, must be a positive-integer with no leading zero
        byte[] pointer = "1+0"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_PlusAtEnd_ReturnsFalse()
    {
        // "1+" — + without following digit
        byte[] pointer = "1+"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_MinusAtEnd_ReturnsFalse()
    {
        // "1-" — - without following digit
        byte[] pointer = "1-"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    // ─── L140-143: End of input after origin (no '#' or pointer) ─────────

    [TestMethod]
    public void ValidateRelative_OriginOnly_IsValid()
    {
        // "0" — just origin, no '#' or pointer (valid per RFC)
        byte[] pointer = "0"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_MultiDigitOriginOnly_IsValid()
    {
        // "123" — multi-digit origin, no '#' or pointer
        byte[] pointer = "123"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    // ─── L146-149: '#' at end ────────────────────────────────────────────

    [TestMethod]
    public void ValidateRelative_HashOnly_IsValid()
    {
        // "0#" — origin 0 followed by '#' (valid)
        byte[] pointer = "0#"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_HashNotAtEnd_ReturnsFalse()
    {
        // "0#x" — '#' not at end is invalid
        byte[] pointer = "0#x"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    // ─── L151-155: '/' starts a json-pointer suffix ──────────────────────

    [TestMethod]
    public void ValidateRelative_OriginWithPointerSuffix_IsValid()
    {
        // "1/foo/bar" — origin 1, then json-pointer /foo/bar
        byte[] pointer = "1/foo/bar"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    // ─── L157-160: Dead code (i == length checked at L140 first) ─────────

    // L157 checks `i == length` but L140 already does this and returns true.
    // This code is unreachable — confirmed dead code.

    // ─── L162-165: Invalid character after origin/index-manipulation ──────

    [TestMethod]
    public void ValidateRelative_InvalidCharAfterOrigin_ReturnsFalse()
    {
        // "1x" — invalid char 'x' after origin
        byte[] pointer = "1x"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_EmptyInput_ReturnsFalse()
    {
        // Empty span is invalid (must have at least one digit for origin)
        byte[] pointer = [];
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    [TestMethod]
    public void ValidateRelative_NonDigitStart_ReturnsFalse()
    {
        // Starting with non-digit is invalid
        byte[] pointer = "x"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointerTools.ValidateRelative(pointer));
    }

    // ─── L194-197: DecodePointer escape at end of segment ────────────────

    [TestMethod]
    public void DecodeSegment_TildeAtEnd_Throws()
    {
        // "foo~" — ~ at end without 0 or 1
        byte[] encoded = "foo~"u8.ToArray();
        byte[] decoded = new byte[encoded.Length];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Utf8JsonPointer.DecodeSegment(encoded, decoded));
    }

    // ─── L207-210: DecodePointer invalid escape digit ────────────────────

    [TestMethod]
    public void DecodeSegment_TildeFollowedByInvalidDigit_Throws()
    {
        // "foo~2" — ~2 is not valid (only ~0 and ~1)
        byte[] encoded = "foo~2"u8.ToArray();
        byte[] decoded = new byte[encoded.Length];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Utf8JsonPointer.DecodeSegment(encoded, decoded));
    }

    // ─── Decode: valid escapes (positive path) ───────────────────────────

    [TestMethod]
    public void DecodeSegment_ValidEscapes_DecodesCorrectly()
    {
        // "a~0b~1c" → "a~b/c"
        byte[] encoded = "a~0b~1c"u8.ToArray();
        byte[] decoded = new byte[encoded.Length];

        int written = Utf8JsonPointer.DecodeSegment(encoded, decoded);
        string result = JsonReaderHelper.TranscodeHelper(decoded.AsSpan(0, written));
        Assert.AreEqual("a~b/c", result);
    }

    [TestMethod]
    public void DecodeSegment_NoEscapes_CopiesToOutput()
    {
        // "hello" — no escapes, straight copy
        byte[] encoded = "hello"u8.ToArray();
        byte[] decoded = new byte[encoded.Length];

        int written = Utf8JsonPointer.DecodeSegment(encoded, decoded);
        string result = JsonReaderHelper.TranscodeHelper(decoded.AsSpan(0, written));
        Assert.AreEqual("hello", result);
    }

    // ─── L28-29: Empty pointer is valid (root) ──────────────────────────

    [TestMethod]
    public void Validate_EmptySpan_ReturnsTrue()
    {
        // Empty string is a valid JSON pointer (references root)
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer([], out _));
    }

    // ─── L36-37: Pointer not starting with '/' is invalid ───────────────

    [TestMethod]
    public void Validate_NotStartingWithSlash_ReturnsFalse()
    {
        // "foo" — doesn't start with '/' → invalid
        byte[] pointer = "foo"u8.ToArray();
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    // ─── L59-60: Valid escape sequence (~0 and ~1) in Validate ───────────

    [TestMethod]
    public void Validate_ValidEscapeSequences_ReturnsTrue()
    {
        // "/a~0b~1c" — ~0 = tilde, ~1 = slash → valid
        byte[] pointer = "/a~0b~1c"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    // ─── L83-84: Valid multi-byte UTF-8 character in segment ─────────────

    [TestMethod]
    public void Validate_ValidUtf8MultiByte_ReturnsTrue()
    {
        // "/café" in UTF-8: 'é' is 0xC3 0xA9 (2-byte sequence)
        byte[] pointer = "/caf\u00e9"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    [TestMethod]
    public void Validate_ValidUtf8ThreeByte_ReturnsTrue()
    {
        // "/日本" — 3-byte CJK characters
        byte[] pointer = "/\u65e5\u672c"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }

    [TestMethod]
    public void Validate_ValidUtf8FourByte_ReturnsTrue()
    {
        // "/\U0001F600" — 4-byte emoji (U+1F600)
        byte[] pointer = "/\U0001F600"u8.ToArray();
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer(pointer, out _));
    }
}
