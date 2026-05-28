// <copyright file="Utf8JsonPointerEncodeSegmentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="Utf8JsonPointer.TryEncodeSegment"/>.
/// </summary>
[TestClass]
public class Utf8JsonPointerEncodeSegmentTests
{
    // ─── Basic encoding ──────────────────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_NoSpecialChars_CopiesVerbatim()
    {
        ReadOnlySpan<byte> raw = "hello"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("hello", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_TildeEscapedToTilde0()
    {
        ReadOnlySpan<byte> raw = "a~b"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("a~0b", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_SlashEscapedToTilde1()
    {
        ReadOnlySpan<byte> raw = "a/b"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("a~1b", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_BothTildeAndSlash()
    {
        // "~/" → "~0~1"
        ReadOnlySpan<byte> raw = "~/"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("~0~1", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_MultipleEscapes()
    {
        // "/pets/{petId}" → "~1pets~1{petId}"
        ReadOnlySpan<byte> raw = "/pets/{petId}"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("~1pets~1{petId}", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    // ─── Empty input ─────────────────────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_EmptyInput_WritesZeroBytes()
    {
        ReadOnlySpan<byte> raw = ReadOnlySpan<byte>.Empty;
        Span<byte> encoded = stackalloc byte[16];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    // ─── Destination too small ───────────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_DestinationTooSmall_ReturnsFalse()
    {
        // "a/b" needs 4 bytes ("a~1b"), but we only provide 3
        ReadOnlySpan<byte> raw = "a/b"u8;
        Span<byte> encoded = stackalloc byte[3];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryEncodeSegment_DestinationExactSize_Succeeds()
    {
        // "a~b" → "a~0b" needs exactly 4 bytes
        ReadOnlySpan<byte> raw = "a~b"u8;
        Span<byte> encoded = stackalloc byte[4];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(4, written);
        Assert.AreEqual("a~0b", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_DestinationSmallerThanInput_ReturnsFalse()
    {
        // Even with no special chars, destination must fit the input
        ReadOnlySpan<byte> raw = "hello"u8;
        Span<byte> encoded = stackalloc byte[3];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out _);

        Assert.IsFalse(result);
    }

    // ─── All tildes ──────────────────────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_AllTildes_DoublesLength()
    {
        // "~~~" → "~0~0~0" (3 bytes → 6 bytes)
        ReadOnlySpan<byte> raw = "~~~"u8;
        Span<byte> encoded = stackalloc byte[6];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(6, written);
        Assert.AreEqual("~0~0~0", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    // ─── All slashes ─────────────────────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_AllSlashes_DoublesLength()
    {
        // "///" → "~1~1~1" (3 bytes → 6 bytes)
        ReadOnlySpan<byte> raw = "///"u8;
        Span<byte> encoded = stackalloc byte[6];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(6, written);
        Assert.AreEqual("~1~1~1", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    // ─── UTF-8 multi-byte characters pass through ────────────────────────

    [TestMethod]
    public void TryEncodeSegment_Utf8MultiByte_PreservedVerbatim()
    {
        // "café" — 'é' is 0xC3 0xA9, should pass through unchanged
        ReadOnlySpan<byte> raw = "caf\u00e9"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("caf\u00e9", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_Utf8FourByte_PreservedVerbatim()
    {
        // Emoji U+1F600 — 4-byte UTF-8, no escaping needed
        ReadOnlySpan<byte> raw = "\U0001F600"u8;
        Span<byte> encoded = stackalloc byte[raw.Length * 2];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(4, written);
    }

    // ─── Roundtrip: Encode then Decode ───────────────────────────────────

    [TestMethod]
    [DataRow("hello")]
    [DataRow("a/b")]
    [DataRow("a~b")]
    [DataRow("~/~")]
    [DataRow("/pets/{petId}")]
    [DataRow("application/json")]
    [DataRow("")]
    public void TryEncodeSegment_RoundtripsWithDecodeSegment(string raw)
    {
        byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(raw);
        Span<byte> encoded = stackalloc byte[rawBytes.Length * 2 + 1];

        bool encodeResult = Utf8JsonPointer.TryEncodeSegment(rawBytes, encoded, out int encodedLen);
        Assert.IsTrue(encodeResult);

        Span<byte> decoded = stackalloc byte[encodedLen + 1];
        int decodedLen = Utf8JsonPointer.DecodeSegment(encoded[..encodedLen], decoded);

        Assert.AreEqual(rawBytes.Length, decodedLen);
        Assert.IsTrue(rawBytes.AsSpan().SequenceEqual(decoded[..decodedLen]));
    }

    // ─── RFC 6901 Section 5 examples ─────────────────────────────────────

    [TestMethod]
    public void TryEncodeSegment_Rfc6901_SlashInPropertyName()
    {
        // Property name "a/b" must encode to "a~1b"
        ReadOnlySpan<byte> raw = "a/b"u8;
        Span<byte> encoded = stackalloc byte[8];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("a~1b", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }

    [TestMethod]
    public void TryEncodeSegment_Rfc6901_TildeInPropertyName()
    {
        // Property name "m~n" must encode to "m~0n"
        ReadOnlySpan<byte> raw = "m~n"u8;
        Span<byte> encoded = stackalloc byte[8];

        bool result = Utf8JsonPointer.TryEncodeSegment(raw, encoded, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual("m~0n", JsonReaderHelper.TranscodeHelper(encoded[..written]));
    }
}