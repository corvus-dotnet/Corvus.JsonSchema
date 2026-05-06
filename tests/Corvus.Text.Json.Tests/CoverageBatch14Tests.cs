// Copyright (c) Endjin Limited. All rights reserved.

using System.Buffers;
using System.Linq;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 14: JsonCanonicalizer edge cases, JsonHelpers validation,
/// and Utf8JsonWriter WriteRawValue sequence paths.
/// </summary>
public static class CoverageBatch14Tests
{
    #region JsonHelpers.ValidateInt32MaxArrayLength overflow (line 115-116)

    /// <summary>
    /// ValidateInt32MaxArrayLength throws OutOfMemoryException for values > 0x7FEFFFFF.
    /// Target: JsonHelpers.cs lines 115-116.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void ValidateInt32MaxArrayLength_ExceedsMax_ThrowsOutOfMemory()
    {
        Assert.Throws<OutOfMemoryException>(() => JsonHelpers.ValidateInt32MaxArrayLength(0x7FF00000));
    }

    #endregion

    #region JsonCanonicalizer overflow path (line 119, 293-295)

    /// <summary>
    /// TryCanonicalize with a destination too small triggers the overflow path
    /// in WriteElement (early return) and WriteByte.
    /// Target: JsonCanonicalizer.cs line 119 (WriteElement early return).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonCanonicalizer_BufferTooSmall_ReturnsOverflow()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":123456}"""u8.ToArray());

        // 1 byte is far too small for any meaningful output
        Span<byte> tiny = stackalloc byte[1];
        bool result = JsonCanonicalizer.TryCanonicalize(doc.RootElement, tiny, out int written);
        Assert.False(result);
    }

    /// <summary>
    /// TryCanonicalize with a destination just big enough for property name but too small for
    /// the number value triggers the TryFormat failure path inside WriteNumber.
    /// Target: JsonCanonicalizer.cs lines 293-295 (Es6NumberFormatter.TryFormat overflow).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonCanonicalizer_NumberOverflow_SetOverflowFlag()
    {
        // {"a":123456} canonical form is {"a":123456} = 14 bytes
        // {"a": is 5 bytes, "123456" is 6 bytes, "}" is 1 byte = 12 bytes total
        // Provide enough space for {"a": but not enough for the number
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":123456}"""u8.ToArray());
        Span<byte> buffer = stackalloc byte[8]; // enough for {"a": (5 bytes) but only 3 bytes left for 123456 (needs 6)
        bool result = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int written);
        Assert.False(result);
    }

    #endregion

    #region JsonCanonicalizer dead code documentation

    // Line 124: depth > MaxDepth (64) check is unreachable because the Utf8JsonReader
    // enforces its own max depth of 64 during parsing. ParsedJsonDocument.Parse throws
    // JsonReaderException before a 65+ deep element can be constructed.
    //
    // Lines 283-284: overflow check in WriteNumber is dead code because WriteElement
    // already checks overflow at lines 117-119 before dispatching to WriteNumber.
    // WriteNumber can only be called when overflow is false.
    //
    // Line 151: default case in value kind switch — all valid JSON value kinds are handled.
    // Lines 272-274: TryGetDouble failure — TryGetDouble always succeeds for valid JSON numbers.
    // Lines 277-279: NaN/Infinity check — valid JSON cannot produce NaN/Infinity from TryGetDouble.

    #endregion

    #region Utf8JsonWriter WriteRawValue — empty ReadOnlySequence (line 150-151)

    /// <summary>
    /// WriteRawValue with an empty ReadOnlySequence throws ArgumentException.
    /// Target: Utf8JsonWriter.WriteValues.Raw.cs lines 150-151.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteRawValue_EmptySequence_Throws()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        var emptySequence = new ReadOnlySequence<byte>(Array.Empty<byte>());
        Assert.Throws<ArgumentException>(() => writer.WriteRawValue(emptySequence));
    }

    #endregion

    #region Utf8JsonWriter WriteRawValue — ReadOnlySequence single segment (line 190-191)

    /// <summary>
    /// WriteRawValue with a non-empty ReadOnlySequence after previous values triggers list separator.
    /// Target: Utf8JsonWriter.WriteValues.Raw.cs lines 190-191.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteRawValue_Sequence_WithListSeparator()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        var seq1 = new ReadOnlySequence<byte>("42"u8.ToArray());
        writer.WriteRawValue(seq1, skipInputValidation: true);
        var seq2 = new ReadOnlySequence<byte>("99"u8.ToArray());
        writer.WriteRawValue(seq2, skipInputValidation: true);
        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Equal("[42,99]", result);
    }

    #endregion

    #region Utf8JsonWriter WriteValues.Double — Grow path (lines 57-59)

    /// <summary>
    /// Writing many double values to a writer triggers the buffer grow path.
    /// Target: Utf8JsonWriter.WriteValues.Double.cs lines 57-59.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteDoubleValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        // Write enough doubles to force buffer growth
        for (int i = 0; i < 20; i++)
        {
            writer.WriteNumberValue(3.14159265358979);
        }

        writer.WriteEndArray();
        writer.Flush();

        // Verify output starts with [ and ends with ]
        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion

    #region Utf8JsonWriter WriteValues.Float — Grow path (lines 57-59)

    /// <summary>
    /// Writing many float values to a writer triggers the buffer grow path.
    /// Target: Utf8JsonWriter.WriteValues.Float.cs lines 57-59.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteFloatValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        // Initial buffer is 256 bytes. maxRequired=129, so Grow triggers when BytesPending > 127.
        // Each float is ~5 bytes, so need 127/5 ≈ 26 floats.
        for (int i = 0; i < 30; i++)
        {
            writer.WriteNumberValue(3.14f);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion
}
