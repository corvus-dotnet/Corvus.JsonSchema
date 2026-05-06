// Copyright (c) Endjin Limited. All rights reserved.

#if NET

using System;
using System.Buffers;
using System.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 21: Utf8JsonWriter large escape ArrayPool paths in WriteProperties.String,
/// and WriteStringSegmentEpilogue Grow path.
/// </summary>
public static class CoverageBatch21Tests
{
    #region WriteStringEscapePropertyOrValue(char propName, byte utf8Value) — lines 981-985, 1001-1005

    /// <summary>
    /// Writing a string property with a char property name and UTF-8 value where the escaped value
    /// exceeds StackallocByteThreshold (256) triggers ArrayPool rent for the value buffer.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 981-985.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_CharPropByteValue_LargeEscapedValue_TriggersArrayPool()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(4096);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // 50 bytes of '<' (each escapes to \u003C = 6 bytes → 300 > 256 threshold)
        byte[] utf8Value = new byte[50];
        Array.Fill(utf8Value, (byte)'<');

        // Char property name that does NOT need escaping
        writer.WriteString("prop".AsSpan(), utf8Value);

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\"prop\":", json);
        // Each '<' → \u003C in the escaped output
        Assert.Contains("\\u003C", json);
    }

    /// <summary>
    /// Writing a string property with a char property name where the escaped property name
    /// exceeds StackallocCharThreshold (128) triggers ArrayPool rent for the property name buffer.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1001-1005.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_CharPropByteValue_LargeEscapedPropertyName_TriggersArrayPool()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(8192);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // 25 chars of '<' (each escapes to \u003C = 6 chars → 150 > 128 threshold)
        char[] propName = new char[25];
        Array.Fill(propName, '<');

        // Simple UTF-8 value that needs no escaping
        writer.WriteString(propName.AsSpan(), "hello"u8);

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\\u003C", json);
        Assert.Contains("\"hello\"", json);
    }

    #endregion

    #region WriteStringEscapePropertyOrValue(byte utf8PropName, char value) — lines 1041-1045, 1062-1065

    /// <summary>
    /// Writing a string property with a UTF-8 property name where the escaped property name
    /// exceeds StackallocByteThreshold (256) triggers ArrayPool rent for the property name buffer.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1062-1065.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_BytePropCharValue_LargeEscapedPropertyName_TriggersArrayPool()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(8192);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // 50 bytes of '<' (each escapes to \u003C = 6 bytes → 300 > 256 threshold)
        byte[] utf8PropName = new byte[50];
        Array.Fill(utf8PropName, (byte)'<');

        // Simple char value
        writer.WriteString(utf8PropName.AsSpan(), "world".AsSpan());

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\\u003C", json);
        Assert.Contains("\"world\"", json);
    }

    /// <summary>
    /// Writing a string property with a char value where the escaped value
    /// exceeds StackallocCharThreshold (128) triggers ArrayPool rent for the value buffer.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1041-1045.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_BytePropCharValue_LargeEscapedValue_TriggersArrayPool()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(8192);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // Simple UTF-8 property name
        writer.WriteString("key"u8, new string('<', 25).AsSpan());

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\"key\":", json);
        Assert.Contains("\\u003C", json);
    }

    #endregion

    #region WriteStringSegmentEpilogue Grow — lines 308-310

    /// <summary>
    /// Writing a string segment where the buffer is exactly at capacity when the epilogue
    /// (closing quote) needs to be written triggers a Grow call.
    /// Target: Utf8JsonWriter.WriteValues.StringSegment.cs lines 308-310.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_StringSegmentEpilogue_TriggerGrow()
    {
        // Use a tight buffer writer that returns exactly the requested size from GetMemory.
        // InitialGrowthSize is 256, so the first memory is exactly 256 bytes.
        // After writing '[' (1 byte) and '"' (prologue, 1 byte), BytesPending = 2.
        // Writing 254 bytes of non-escaped UTF-8 data fills it to: BytesPending = 256 = _memory.Length.
        // Then when WriteStringSegmentEpilogue tries to write '"', the condition
        // _memory.Length == BytesPending is true, triggering Grow(1).
        var bufferWriter = new TightBufferWriter();
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();

        // Write 254 bytes of non-escaped content as a non-final segment to fill the buffer
        byte[] chunk = new byte[254];
        Array.Fill(chunk, (byte)'A');
        writer.WriteStringValueSegment(chunk, isFinalSegment: false);

        // Now the final (empty) segment triggers the epilogue with buffer exactly full
        writer.WriteStringValueSegment(ReadOnlySpan<byte>.Empty, isFinalSegment: true);

        writer.WriteEndArray();
        writer.Flush();

        string result = Encoding.UTF8.GetString(bufferWriter.WrittenData.ToArray());
        Assert.StartsWith("[\"", result);
        Assert.EndsWith("\"]", result);
        Assert.Equal(254, result.AsSpan().Count('A'));
    }

    /// <summary>
    /// A tight buffer writer that provides exactly the minimum requested memory.
    /// This helps trigger edge cases where BytesPending == _memory.Length.
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

            // Return EXACTLY the requested size — no extra headroom.
            return _buffer.AsMemory(_index, needed);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            int needed = Math.Max(sizeHint, 1);
            EnsureCapacity(needed);

            // Return EXACTLY the requested size — no extra headroom.
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

    #region Both byte prop + byte value large escapes — lines 908-965 (complementary coverage)

    /// <summary>
    /// Writing string property with both UTF-8 property name and UTF-8 value needing large escape
    /// exercises the WriteStringEscapePropertyOrValue(byte, byte) overload with both pools.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_BothUtf8_LargeEscapes_TriggersArrayPools()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(16384);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // Both property name and value need escaping and exceed 256 bytes
        byte[] utf8PropName = new byte[50];
        Array.Fill(utf8PropName, (byte)'<');

        byte[] utf8Value = new byte[50];
        Array.Fill(utf8Value, (byte)'>');

        writer.WriteString(utf8PropName.AsSpan(), utf8Value.AsSpan());

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\\u003C", json);
        Assert.Contains("\\u003E", json);
    }

    /// <summary>
    /// Writing string property with both char property name and char value needing large escape
    /// exercises the WriteStringEscapePropertyOrValue(char, char) overload with both pools.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Writer_BothChar_LargeEscapes_TriggersArrayPools()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(16384);
        using var writer = new Utf8JsonWriter(bufferWriter);

        writer.WriteStartObject();

        // Both need escaping and exceed 128 chars threshold
        char[] propName = new char[25];
        Array.Fill(propName, '<');

        char[] value = new char[25];
        Array.Fill(value, '>');

        writer.WriteString(propName.AsSpan(), value.AsSpan());

        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        Assert.Contains("\\u003C", json);
        Assert.Contains("\\u003E", json);
    }

    #endregion
}

#endif
