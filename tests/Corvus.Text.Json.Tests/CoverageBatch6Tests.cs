// <copyright file="CoverageBatch6Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 6: targeting JsonCanonicalizer number-too-large-for-double,
/// Utf8JsonWriter WriteRawValue (empty sequence, comma-before-raw),
/// and Es6NumberFormatter edge cases.
/// </summary>
public static class CoverageBatch6Tests
{
    #region JsonCanonicalizer — number too large for double (lines 273-274)

    /// <summary>
    /// Exercises <c>WriteNumber</c> throw when TryGetDouble fails (lines 273-274).
    /// A number like 1e999 is valid JSON but overflows IEEE 754 double.
    /// </summary>
    [Fact]
    public static void Canonicalizer_NumberOverflowsDouble_ThrowsInvalidOperation()
    {
        // 1e999 is valid JSON number syntax but overflows double (becomes Infinity)
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""[1e999]"""u8.ToArray());

        byte[] buffer = new byte[256];
        InvalidOperationException? ex = null;
        try
        {
            JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out _);
        }
        catch (InvalidOperationException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);
        Assert.Contains("Infinity", ex!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Utf8JsonWriter — WriteRawValue empty ReadOnlySequence (lines 150-151)

    /// <summary>
    /// Exercises <c>WriteRawValue(ReadOnlySequence&lt;byte&gt;)</c> with empty sequence (lines 150-151).
    /// </summary>
    [Fact]
    public static void Writer_WriteRawValue_EmptySequence_ThrowsArgument()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        ReadOnlySequence<byte> emptySequence = ReadOnlySequence<byte>.Empty;

        Assert.Throws<ArgumentException>(() => writer.WriteRawValue(emptySequence));
    }

    #endregion

    #region Utf8JsonWriter — WriteRawValue comma before value (lines 190-192)

    /// <summary>
    /// Exercises the comma-before-raw-value path (lines 189-192).
    /// When writing a raw value as a non-first element in an array,
    /// _currentDepth &lt; 0 triggers writing a list separator.
    /// </summary>
    [Fact]
    public static void Writer_WriteRawValue_CommaBeforeRawInArray()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        writer.WriteStartArray();
        writer.WriteNumberValue(1); // First element — sets flag for separator

        // Second element via raw — _currentDepth < 0 triggers comma
        ReadOnlySequence<byte> rawValue = new("""42"""u8.ToArray());
        writer.WriteRawValue(rawValue, skipInputValidation: true);

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("[1,42]", json);
    }

    #endregion

    #region Es6NumberFormatter — buffer too small for zero (lines 46-47)

    /// <summary>
    /// Exercises <c>Es6NumberFormatter.TryFormat</c> with a zero-length destination (lines 46-47).
    /// The value 0.0 requires at least 1 byte ("0"), so an empty buffer returns false.
    /// </summary>
    [Fact]
    public static void Es6NumberFormatter_ZeroWithEmptyBuffer_ReturnsFalse()
    {
        Span<byte> empty = Span<byte>.Empty;
        bool result = Es6NumberFormatter.TryFormat(0.0, empty, out int bytesWritten);
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Utf8JsonWriter — WriteRawValue with ReadOnlySpan after another value

    /// <summary>
    /// Exercises WriteRawValue with span after another value in array (verifying
    /// the comma separator in the Span overload path).
    /// </summary>
    [Fact]
    public static void Writer_WriteRawValue_SpanCommaInArray()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        writer.WriteStartArray();
        writer.WriteStringValue("hello"u8);

        // Write raw value as second element (includes quotes because raw is literal)
        writer.WriteRawValue("\"world\""u8, skipInputValidation: true);

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("""["hello","world"]""", json);
    }

    #endregion

    #region JsonCanonicalizer — Canonicalize with small document (stack-alloc path)

    /// <summary>
    /// Exercises <c>Canonicalize</c> with a small document that fits in stackalloc (lines 60-61).
    /// </summary>
    [Fact]
    public static void Canonicalizer_SmallDocument_CanonicalizeOnStack()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"b":2,"a":1}"""u8.ToArray());

        byte[] result = JsonCanonicalizer.Canonicalize(doc.RootElement);

        // JCS sorts property names
        string canonical = Encoding.UTF8.GetString(result);
        Assert.Equal("""{"a":1,"b":2}""", canonical);
    }

    #endregion

    #region JsonCanonicalizer — Canonicalize with large document (ArrayPool growth path)

    /// <summary>
    /// Exercises <c>Canonicalize</c> (non-Try) with a document larger than 256 bytes,
    /// triggering the ArrayPool rent/retry loop (lines 65-82).
    /// The document must be larger than 512 bytes canonical output to trigger
    /// the buffer-doubling path at line 77.
    /// </summary>
    [Fact]
    public static void Canonicalizer_LargeDocument_CanonicalizeSucceeds()
    {
        // Create a JSON object with enough properties to exceed 512 bytes canonical output
        // Each property "property000":0 is ~16 chars, need >32 to exceed 512
        StringBuilder sb = new();
        sb.Append('{');
        for (int i = 0; i < 50; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"property{i:D3}\":{i}");
        }

        sb.Append('}');

        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(sb.ToString()));

        byte[] result = JsonCanonicalizer.Canonicalize(doc.RootElement);

        // Result should be valid JSON with sorted property names
        Assert.True(result.Length > 512);
        // First properties should be sorted: property000, property001, etc.
        string canonical = Encoding.UTF8.GetString(result);
        Assert.StartsWith("{\"property000\":", canonical);
    }

    #endregion

    #region Utf8JsonWriter — WriteRawValue(ReadOnlySequence) with validation

    /// <summary>
    /// Exercises <c>WriteRawValue(ReadOnlySequence&lt;byte&gt;)</c> with validation enabled
    /// (the non-skipInputValidation path uses Utf8JsonReader to validate, lines 167-173).
    /// </summary>
    [Fact]
    public static void Writer_WriteRawValue_Sequence_WithValidation()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        writer.WriteStartArray();
        writer.WriteNumberValue(1);

        // Raw value with validation enabled (default) — second element triggers comma
        ReadOnlySequence<byte> rawValue = new("""{"key":"val"}"""u8.ToArray());
        writer.WriteRawValue(rawValue, skipInputValidation: false);

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("""[1,{"key":"val"}]""", json);
    }

    #endregion
}
