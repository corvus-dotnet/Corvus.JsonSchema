// Copyright (c) Endjin Limited. All rights reserved.

#if NET

using System;
using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 22: JsonReaderHelper transcoding error paths,
/// Utf8JsonWriter ulong Grow path, and miscellaneous coverage gaps.
/// </summary>
public static class CoverageBatch22Tests
{
    #region JsonReaderHelper.TryGetUtf8FromText - EncoderFallbackException (lines 479-482)

    /// <summary>
    /// TryGetUtf8FromText with unpaired high surrogate returns false.
    /// Target: JsonReaderHelper.Unescaping.cs lines 479-482.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetUtf8FromText_UnpairedHighSurrogate_ReturnsFalse()
    {
        // Create text with an unpaired high surrogate (U+D800 without a low surrogate)
        char[] invalidChars = ['A', '\uD800', 'B'];
        Span<byte> dest = stackalloc byte[64];

        bool result = JsonReaderHelper.TryGetUtf8FromText(invalidChars, dest, out int written);

        Assert.False(result);
        Assert.Equal(0, written);
    }

    /// <summary>
    /// TryGetUtf8FromText with unpaired low surrogate returns false.
    /// Target: JsonReaderHelper.Unescaping.cs lines 479-482.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetUtf8FromText_UnpairedLowSurrogate_ReturnsFalse()
    {
        // Create text with an unpaired low surrogate (U+DC00 without a preceding high surrogate)
        char[] invalidChars = ['X', '\uDC00', 'Y'];
        Span<byte> dest = stackalloc byte[64];

        bool result = JsonReaderHelper.TryGetUtf8FromText(invalidChars, dest, out int written);

        Assert.False(result);
        Assert.Equal(0, written);
    }

    #endregion

    #region JsonReaderHelper.TryGetTextFromUtf8 - DecoderFallbackException (lines 518-521)

    /// <summary>
    /// TryGetTextFromUtf8 with invalid UTF-8 continuation byte returns false.
    /// Target: JsonReaderHelper.Unescaping.cs lines 518-521.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetTextFromUtf8_InvalidContinuationByte_ReturnsFalse()
    {
        // 0xC0 0x80 is an overlong encoding (invalid UTF-8)
        byte[] invalidUtf8 = [0xC0, 0x80];
        Span<char> dest = stackalloc char[64];

        bool result = JsonReaderHelper.TryGetTextFromUtf8(invalidUtf8, dest, out int written);

        Assert.False(result);
        Assert.Equal(0, written);
    }

    /// <summary>
    /// TryGetTextFromUtf8 with truncated multi-byte sequence returns false.
    /// Target: JsonReaderHelper.Unescaping.cs lines 518-521.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetTextFromUtf8_TruncatedSequence_ReturnsFalse()
    {
        // 0xF0 starts a 4-byte sequence but we only provide 2 bytes
        byte[] invalidUtf8 = [0x41, 0xF0, 0x90];
        Span<char> dest = stackalloc char[64];

        bool result = JsonReaderHelper.TryGetTextFromUtf8(invalidUtf8, dest, out int written);

        Assert.False(result);
        Assert.Equal(0, written);
    }

    #endregion

    #region JsonReaderHelper.GetUtf16CharCount empty input (lines 384-385)

    /// <summary>
    /// GetUtf16CharCount with empty span returns 0.
    /// Target: JsonReaderHelper.Unescaping.cs lines 384-385.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetUtf16CharCount_EmptySpan_ReturnsZero()
    {
        int count = JsonReaderHelper.GetUtf16CharCount(ReadOnlySpan<byte>.Empty);

        Assert.Equal(0, count);
    }

    #endregion

    #region Utf8JsonWriter.WriteNumberValue(ulong) Grow path (lines 103-105)

    /// <summary>
    /// Writing a ulong value when the internal buffer is near-full triggers Grow.
    /// Target: Utf8JsonWriter.WriteValues.UnsignedNumber.cs lines 103-105.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_UlongValue_TriggerGrow()
    {
        // Use TightBufferWriter so the writer buffer is exactly 256 bytes.
        // After '[' (1 byte) + 12 ulong.MaxValue writes (20+11*21 = 251 bytes),
        // BytesPending = 252, free = 4. The 13th ulong needs 21 bytes (maxRequired),
        // so 4 < 21 triggers Grow at line 103-105.
        var bufferWriter = new TightBufferWriter();
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();

        // Write 12 ulong.MaxValue values to fill the 256-byte buffer
        for (int i = 0; i < 12; i++)
        {
            writer.WriteNumberValue(ulong.MaxValue);
        }

        // This 13th write should trigger Grow in the ulong path
        writer.WriteNumberValue(ulong.MaxValue);

        writer.WriteEndArray();
        writer.Flush();

        string result = Encoding.UTF8.GetString(bufferWriter.WrittenData.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);

        // Verify all 13 instances of ulong.MaxValue are present
        int count = 0;
        int idx = 0;
        while ((idx = result.IndexOf("18446744073709551615", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += 20;
        }

        Assert.Equal(13, count);
    }

    #endregion

    #region JsonReaderHelper.TranscodeHelper valid paths

    /// <summary>
    /// TranscodeHelper with empty span returns empty string.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TranscodeHelper_EmptyInput_ReturnsEmptyString()
    {
        string result = JsonReaderHelper.TranscodeHelper(ReadOnlySpan<byte>.Empty);

        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// TranscodeHelper with ASCII bytes returns correct string.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TranscodeHelper_AsciiBytes_ReturnsCorrectString()
    {
        byte[] utf8 = "Hello, World!"u8.ToArray();

        string result = JsonReaderHelper.TranscodeHelper(utf8);

        Assert.Equal("Hello, World!", result);
    }

    /// <summary>
    /// TranscodeHelper with multi-byte UTF-8 returns correct string.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TranscodeHelper_MultiBytUtf8_ReturnsCorrectString()
    {
        // "café" in UTF-8: 63 61 66 C3 A9
        byte[] utf8 = [0x63, 0x61, 0x66, 0xC3, 0xA9];

        string result = JsonReaderHelper.TranscodeHelper(utf8);

        Assert.Equal("café", result);
    }

    #endregion

    #region Utf8JsonWriterCache recursive path (lines 125-128, 199-203)

    /// <summary>
    /// Renting a second writer from JsonWorkspace while the first is still rented
    /// triggers the recursive allocation path in Utf8JsonWriterCache.
    /// Target: Utf8JsonWriterCache.cs lines 125-128 (RentWriter recursive), 199-203 (RentWriterAndBuffer recursive).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void WriterCache_RecursiveRent_AllocatesFreshInstances()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // First rent — uses cached instance (depth 0)
        Utf8JsonWriter writer1 = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer1);

        // Second rent while first is outstanding — triggers recursive path (depth > 0)
        Utf8JsonWriter writer2 = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer2);

        // Verify both writers are distinct and functional
        Assert.NotSame(writer1, writer2);

        writer1.WriteStartObject();
        writer1.WriteEndObject();
        writer1.Flush();

        writer2.WriteStartArray();
        writer2.WriteEndArray();
        writer2.Flush();

        Assert.Equal("{}"u8.ToArray(), buffer1.WrittenMemory.ToArray());
        Assert.Equal("[]"u8.ToArray(), buffer2.WrittenMemory.ToArray());

        // Return in reverse order (LIFO)
        workspace.ReturnWriterAndBuffer(writer2, buffer2);
        workspace.ReturnWriterAndBuffer(writer1, buffer1);
    }

    /// <summary>
    /// Renting a second writer via RentWriter (without buffer) while the first is still rented
    /// triggers the recursive allocation path in Utf8JsonWriterCache.RentWriter.
    /// Target: Utf8JsonWriterCache.cs lines 125-128.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void WriterCache_RecursiveRentWriter_AllocatesFreshInstance()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        var bufferWriter1 = new ArrayBufferWriter<byte>(256);
        var bufferWriter2 = new ArrayBufferWriter<byte>(256);

        // First rent — uses cached instance
        Utf8JsonWriter writer1 = workspace.RentWriter(bufferWriter1);

        // Second rent while first is outstanding — recursive path
        Utf8JsonWriter writer2 = workspace.RentWriter(bufferWriter2);

        Assert.NotSame(writer1, writer2);

        writer1.WriteStartObject();
        writer1.WriteEndObject();
        writer1.Flush();

        writer2.WriteNumberValue(42);
        writer2.Flush();

        Assert.Equal("{}"u8.ToArray(), bufferWriter1.WrittenSpan.ToArray());
        Assert.Equal("42"u8.ToArray(), bufferWriter2.WrittenSpan.ToArray());

        workspace.ReturnWriter(writer2);
        workspace.ReturnWriter(writer1);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A tight buffer writer that provides exactly the minimum requested memory.
    /// This helps trigger edge cases where BytesPending approaches _memory.Length.
    /// </summary>
    private sealed class TightBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buffer = new byte[256];
        private int _index;

        public ReadOnlyMemory<byte> WrittenData => _buffer.AsMemory(0, _index);

        public void Advance(int count)
        {
            _index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            int needed = Math.Max(sizeHint, 1);
            EnsureCapacity(needed);
            return _buffer.AsMemory(_index, needed);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            int needed = Math.Max(sizeHint, 1);
            EnsureCapacity(needed);
            return _buffer.AsSpan(_index, needed);
        }

        private void EnsureCapacity(int needed)
        {
            if (_index + needed > _buffer.Length)
            {
                int newSize = Math.Max(_buffer.Length * 2, _index + needed);
                byte[] newBuffer = new byte[newSize];
                _buffer.AsSpan(0, _index).CopyTo(newBuffer);
                _buffer = newBuffer;
            }
        }
    }

    #endregion
}

#endif
