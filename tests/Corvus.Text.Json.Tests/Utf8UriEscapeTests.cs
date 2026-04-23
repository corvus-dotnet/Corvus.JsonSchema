// <copyright file="Utf8UriEscapeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="Utf8Uri.TryEscapeDataString"/>, <see cref="Utf8Uri.TryUnescapeDataString"/>,
/// and <see cref="Utf8Uri.TryEscapeUriString"/>.
/// </summary>
public class Utf8UriEscapeTests
{
    // ─── TryEscapeDataString ───────────────────────────────────────────

    [Fact]
    public void EscapeDataString_EmptyInput()
    {
        Span<byte> dest = stackalloc byte[16];
        Assert.True(Utf8Uri.TryEscapeDataString(ReadOnlySpan<byte>.Empty, dest, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void EscapeDataString_UnreservedPassThrough()
    {
        // RFC 3986 unreserved: ALPHA DIGIT - . _ ~
        byte[] source = "abcABC012-._~"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        Assert.Equal(source.Length, written);
        Assert.True(source.AsSpan().SequenceEqual(dest.Slice(0, written)));
    }

    [Fact]
    public void EscapeDataString_SpaceIsEncoded()
    {
        byte[] source = "hello world"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("hello%20world", result);
    }

    [Fact]
    public void EscapeDataString_ReservedCharsAreEncoded()
    {
        // Reserved chars should be percent-encoded in EscapeDataString
        byte[] source = ":/?#[]@!$&'()*+,;="u8.ToArray();
        Span<byte> dest = stackalloc byte[256];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());

        // Every reserved character should be encoded as %XX
        Assert.DoesNotContain(":", result.Replace("%3A", ""));
        Assert.DoesNotContain("/", result.Replace("%2F", ""));
        Assert.DoesNotContain("?", result.Replace("%3F", ""));
        // Verify the entire result is percent-encoded (no literal reserved chars)
        foreach (byte b in source)
        {
            string expected = $"%{b:X2}";
            Assert.Contains(expected, result);
        }
    }

    [Fact]
    public void EscapeDataString_PercentIsEncoded()
    {
        byte[] source = "%25"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        // % → %25, 2 → passthrough? No, 2 is a digit (unreserved), 5 is a digit
        Assert.Equal("%2525", result);
    }

    [Fact]
    public void EscapeDataString_NonAsciiMultiByte()
    {
        // "café" in UTF-8: 63 61 66 C3 A9
        byte[] source = "café"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("caf%C3%A9", result);
    }

    [Fact]
    public void EscapeDataString_DestinationTooSmall()
    {
        byte[] source = "hello world"u8.ToArray();
        Span<byte> dest = stackalloc byte[5]; // too small for "hello%20world"
        Assert.False(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void EscapeDataString_ExactDestinationSize()
    {
        // "a b" → "a%20b" = 5 bytes
        byte[] source = "a b"u8.ToArray();
        Span<byte> dest = stackalloc byte[5];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        Assert.Equal(5, written);
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("a%20b", result);
    }

    // ─── TryUnescapeDataString ──────────────────────────────────────────

    [Fact]
    public void UnescapeDataString_EmptyInput()
    {
        Span<byte> dest = stackalloc byte[16];
        Assert.True(Utf8Uri.TryUnescapeDataString(ReadOnlySpan<byte>.Empty, dest, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void UnescapeDataString_NoEscapes()
    {
        byte[] source = "hello-world"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        Assert.Equal(source.Length, written);
        Assert.True(source.AsSpan().SequenceEqual(dest.Slice(0, written)));
    }

    [Fact]
    public void UnescapeDataString_AsciiPercentDecode()
    {
        byte[] source = "hello%20world"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void UnescapeDataString_MultipleAsciiEscapes()
    {
        byte[] source = "%48%65%6C%6C%6F"u8.ToArray(); // Hello
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void UnescapeDataString_ValidMultiByteUtf8()
    {
        // %C3%A9 → é (valid 2-byte UTF-8)
        byte[] source = "caf%C3%A9"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("café", result);
    }

    [Fact]
    public void UnescapeDataString_Valid3ByteUtf8()
    {
        // %E2%82%AC → € (Euro sign, U+20AC, 3-byte UTF-8)
        byte[] source = "%E2%82%AC"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("€", result);
    }

    [Fact]
    public void UnescapeDataString_Valid4ByteUtf8()
    {
        // %F0%9F%98%80 → 😀 (U+1F600, 4-byte UTF-8)
        byte[] source = "%F0%9F%98%80"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("😀", result);
    }

    [Fact]
    public void UnescapeDataString_InvalidMultiByteUtf8_KeepsEncoded()
    {
        // %C3%28 → 0xC3 starts 2-byte UTF-8 but 0x28 is not a continuation byte
        // .NET runtime keeps %C3 encoded and decodes %28 → '('
        byte[] source = "%C3%28"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        // %C3 stays encoded, %28 = '(' is ASCII and gets decoded
        Assert.Equal("%C3(", result);
    }

    [Fact]
    public void UnescapeDataString_TruncatedPercentAtEnd()
    {
        // Truncated % at end of string → copied literally
        byte[] source = "hello%"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("hello%", result);
    }

    [Fact]
    public void UnescapeDataString_TruncatedPercentOneHexDigit()
    {
        // Only one hex digit after % → copied literally
        byte[] source = "hello%2"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("hello%2", result);
    }

    [Fact]
    public void UnescapeDataString_InvalidHexDigits()
    {
        // %GZ is not valid hex → copy '%' literally, then 'G' and 'Z'
        byte[] source = "%GZ"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("%GZ", result);
    }

    [Fact]
    public void UnescapeDataString_Percent25_DecodesPercent()
    {
        // %25 → '%'
        byte[] source = "%25"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("%", result);
    }

    [Fact]
    public void UnescapeDataString_DestinationTooSmall()
    {
        byte[] source = "hello%20world"u8.ToArray();
        Span<byte> dest = stackalloc byte[3]; // too small
        Assert.False(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        Assert.Equal(0, written);
    }

    // ─── Round-trip: Escape → Unescape ──────────────────────────────────

    [Theory]
    [InlineData("hello world")]
    [InlineData("café")]
    [InlineData("foo/bar?baz=1&x=2")]
    [InlineData("😀🎉")]
    [InlineData("a+b=c")]
    [InlineData("100% done")]
    [InlineData("")]
    [InlineData("simple")]
    public void EscapeDataString_RoundTrip(string input)
    {
        byte[] source = Encoding.UTF8.GetBytes(input);
        Span<byte> escaped = stackalloc byte[source.Length * 3];
        Assert.True(Utf8Uri.TryEscapeDataString(source, escaped, out int escapedLen));

        Span<byte> unescaped = stackalloc byte[source.Length + 16];
        Assert.True(Utf8Uri.TryUnescapeDataString(escaped.Slice(0, escapedLen), unescaped, out int unescapedLen));

        Assert.True(source.AsSpan().SequenceEqual(unescaped.Slice(0, unescapedLen)));
    }

    // ─── Differential tests vs System.Uri ────────────────────────────────

    [Theory]
    [InlineData("hello world")]
    [InlineData("café")]
    [InlineData("foo/bar?baz=1&x=2")]
    [InlineData("a+b=c")]
    [InlineData("#fragment")]
    [InlineData("50%")]
    [InlineData("日本語")]
    [InlineData("")]
    public void EscapeDataString_MatchesSystemUri(string input)
    {
        string expected = Uri.EscapeDataString(input);

        byte[] source = Encoding.UTF8.GetBytes(input);
        Span<byte> dest = stackalloc byte[source.Length * 3 + 16];
        Assert.True(Utf8Uri.TryEscapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hello%20world")]
    [InlineData("caf%C3%A9")]
    [InlineData("%E2%82%AC")]
    [InlineData("%F0%9F%98%80")]
    [InlineData("no%escapes")]
    [InlineData("%25")]
    [InlineData("trailing%")]
    [InlineData("%2")]
    [InlineData("%GZ")]
    [InlineData("")]
    public void UnescapeDataString_MatchesSystemUri(string input)
    {
        string expected = Uri.UnescapeDataString(input);

        byte[] source = Encoding.UTF8.GetBytes(input);
        Span<byte> dest = stackalloc byte[source.Length + 16];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());

        Assert.Equal(expected, result);
    }

    // ─── Mixed invalid multi-byte sequences ──────────────────────────────

    [Fact]
    public void UnescapeDataString_LoneHighByteStaysEncoded()
    {
        // %80 alone is not valid UTF-8 lead byte → stays encoded
        byte[] source = "%80"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        Assert.Equal("%80", result);
    }

    [Fact]
    public void UnescapeDataString_OverlongSequenceStaysEncoded()
    {
        // %C0%AF is an overlong encoding of '/' — invalid in modern UTF-8
        byte[] source = "%C0%AF"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        // .NET's Rune.DecodeFromUtf8 rejects overlong sequences
        // Verify it matches System.Uri
        string expected = Uri.UnescapeDataString("%C0%AF");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnescapeDataString_MixedValidInvalidSequences()
    {
        // %C3%A9 (valid é) followed by %80 (invalid lone continuation byte)
        byte[] source = "%C3%A9%80"u8.ToArray();
        Span<byte> dest = stackalloc byte[64];
        Assert.True(Utf8Uri.TryUnescapeDataString(source, dest, out int written));
        string result = Encoding.UTF8.GetString(dest.Slice(0, written).ToArray());
        // é is decoded, %80 stays encoded
        Assert.Equal("é%80", result);
    }
}