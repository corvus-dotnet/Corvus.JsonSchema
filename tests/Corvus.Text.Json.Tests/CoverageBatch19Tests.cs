// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Buffers;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 19: WriteValues.Float minimized Grow, WriteProperties.String Grow paths,
/// WriteProperties.Literal byte minimized Grow, WriteValues.Raw comma path.
/// </summary>
public static class CoverageBatch19Tests
{
    #region WriteValues.Float minimized Grow (lines 57-59)

    /// <summary>
    /// Writing many floats in minimized array mode triggers minimized Grow.
    /// Target: Utf8JsonWriter.WriteValues.Float.cs lines 57-59.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteFloatMinimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();

        // MaximumFormatSingleLength + 1 = 129; buffer ~272 after first Grow.
        // Each float is ~5-10 bytes. Need 30+ to exhaust buffer.
        for (int i = 0; i < 30; i++)
        {
            writer.WriteNumberValue(1.23f + i);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
    }

    #endregion

    #region WriteProperties.Literal byte-overload minimized Grow (lines 446-449)

    /// <summary>
    /// Writing many null properties with JsonEncodedText names triggers byte-overload Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Literal.cs lines 446-449.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteLiteralByteMinimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[20];
        for (int i = 0; i < 20; i++)
        {
            names[i] = JsonEncodedText.Encode($"literal_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 20; i++)
        {
            writer.WriteNull(names[i]);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("literal_prop_0", result);
    }

    #endregion

    #region WriteProperties.String minimized Grow paths

    /// <summary>
    /// WriteString(JsonEncodedText, JsonEncodedText) in minimized mode triggers byte/byte Grow.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1570-1573.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteStringByteByte_MinimizedGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        JsonEncodedText[] values = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"str_prop_{i}");
            values[i] = JsonEncodedText.Encode($"value_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString(names[i], values[i]);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("str_prop_0", result);
    }

    /// <summary>
    /// WriteString(string, JsonEncodedText) in minimized mode triggers char/byte Grow.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1608-1611.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteStringCharByte_MinimizedGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] values = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            values[i] = JsonEncodedText.Encode($"value_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString($"str_char_prop_{i}", values[i]);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("str_char_prop_0", result);
    }

    /// <summary>
    /// WriteString(JsonEncodedText, string) in minimized mode triggers byte/char Grow.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1645-1648.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteStringByteChar_MinimizedGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"str_byte_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString(names[i], $"value_{i}");
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("str_byte_prop_0", result);
    }

    #endregion

    #region WriteProperties.String indented Grow paths

    /// <summary>
    /// WriteString(string, JsonEncodedText) in indented mode triggers char/byte indented Grow.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1350-1353.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteStringCharByte_IndentedGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        JsonEncodedText[] values = new JsonEncodedText[12];
        for (int i = 0; i < 12; i++)
        {
            values[i] = JsonEncodedText.Encode($"value_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString($"indented_char_prop_{i}", values[i]);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("indented_char_prop_0", result);
    }

    /// <summary>
    /// WriteString(JsonEncodedText, string) in indented mode triggers byte/char indented Grow.
    /// Target: Utf8JsonWriter.WriteProperties.String.cs lines 1401-1404.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteStringByteChar_IndentedGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        JsonEncodedText[] names = new JsonEncodedText[12];
        for (int i = 0; i < 12; i++)
        {
            names[i] = JsonEncodedText.Encode($"indented_byte_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString(names[i], $"value_{i}");
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Contains("indented_byte_prop_0", result);
    }

    #endregion

    #region WriteValues.Raw comma path (lines 190-192)

    /// <summary>
    /// Writing multiple raw values in an array triggers the comma insertion path.
    /// Target: Utf8JsonWriter.WriteValues.Raw.cs lines 189-192.
    /// Uses ReadOnlySequence overload which hits the sequence-based write path.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteRawValues_CommaPath()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 256);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();

        // Use ReadOnlySequence<byte> overload to hit the sequence-based write path
        var seq1 = new System.Buffers.ReadOnlySequence<byte>(new byte[] { (byte)'4', (byte)'2' });
        var seq2 = new System.Buffers.ReadOnlySequence<byte>(new byte[] { (byte)'9', (byte)'9' });
        var seq3 = new System.Buffers.ReadOnlySequence<byte>(new byte[] { (byte)'1', (byte)'0', (byte)'0' });

        writer.WriteRawValue(seq1, skipInputValidation: true);
        writer.WriteRawValue(seq2, skipInputValidation: true);
        writer.WriteRawValue(seq3, skipInputValidation: true);
        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Equal("[42,99,100]", result);
    }

    #endregion

    #region WriteRawValue(ReadOnlySequence) empty throws (lines 150-151)

    /// <summary>
    /// WriteRawValue with empty ReadOnlySequence throws ArgumentException.
    /// Target: Utf8JsonWriter.WriteValues.Raw.cs lines 150-151.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteRawValueEmptySequence_Throws()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 256);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        var emptySequence = new System.Buffers.ReadOnlySequence<byte>(Array.Empty<byte>());

        Assert.Throws<ArgumentException>(() => writer.WriteRawValue(emptySequence));
    }

    #endregion

    #region WriteValues.Raw length == int.MaxValue (lines 112-113) — documented dead code

    // Lines 112-113: WriteRawValue(ReadOnlySpan<byte>) with length == int.MaxValue
    // This requires allocating a 2GB+ span, which is impractical in tests.
    // Documented as effectively dead code (defensive guard).

    // Lines 155-156: WriteRawValue(ReadOnlySequence<byte>) with length >= int.MaxValue
    // Same issue — requires 2GB+ sequence. Documented as dead code.

    #endregion

    #region WriteValues.Comment invalid UTF-16 (lines 224-226)

    /// <summary>
    /// WriteComment with invalid UTF-16 (lone surrogate) triggers ThrowArgumentException_InvalidUTF16.
    /// Target: Utf8JsonWriter.WriteValues.Comment.cs lines 224-226 (minimized path).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteCommentInvalidUtf16_Throws()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 256);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        // Create a string with a lone high surrogate (invalid UTF-16)
        char[] invalidChars = new char[] { 'h', 'e', 'l', 'l', 'o', '\xD800' };
        string invalidComment = new string(invalidChars);

        Assert.Throws<ArgumentException>(() => writer.WriteCommentValue(invalidComment));
    }

    #endregion
}
