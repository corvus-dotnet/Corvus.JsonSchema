// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Buffers;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 15: JsonWorkspace edge cases, Utf8JsonWriter Grow paths
/// for Guid, Decimal, DateTime, DateTimeOffset, and Bytes value types.
/// </summary>
public static class CoverageBatch15Tests
{
    #region JsonWorkspace.GetDocument out-of-range (lines 236-237)

    /// <summary>
    /// GetDocument with negative index throws ArgumentOutOfRangeException.
    /// Target: JsonWorkspace.cs lines 236-237.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonWorkspace_GetDocument_NegativeIndex_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetDocument(-1));
    }

    /// <summary>
    /// GetDocument with index beyond length throws ArgumentOutOfRangeException.
    /// Target: JsonWorkspace.cs lines 236-237.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonWorkspace_GetDocument_BeyondLength_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetDocument(0));
    }

    #endregion

    #region JsonWorkspace.Reset resize path (lines 323-326)

    /// <summary>
    /// Reset with a larger capacity than current array triggers the resize path.
    /// Target: JsonWorkspace.cs lines 323-326.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonWorkspace_Reset_LargerCapacity_Resizes()
    {
        // Create workspace, add a document, then reset with larger capacity
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}"""u8.ToArray());
        using var builder1 = doc1.RootElement.CreateBuilder(workspace);

        // Reset with a very large capacity to force array resize
        workspace.Reset(1024, null);

        // Verify workspace is usable after reset
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"y":2}"""u8.ToArray());
        using var builder2 = doc2.RootElement.CreateBuilder(workspace);
        Assert.Equal(JsonValueKind.Object, builder2.RootElement.ValueKind);
    }

    #endregion

    #region JsonWorkspace.Reset clear path (lines 330-332)

    /// <summary>
    /// Reset with same capacity when _length > 0 clears existing documents.
    /// Target: JsonWorkspace.cs lines 330-332.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void JsonWorkspace_Reset_SameCapacity_ClearsDocuments()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}"""u8.ToArray());
        using var builder1 = doc1.RootElement.CreateBuilder(workspace);

        // Reset with small capacity (smaller than current array) to hit the else branch
        workspace.Reset(1, null);

        // Verify workspace is usable after reset
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"y":2}"""u8.ToArray());
        using var builder2 = doc2.RootElement.CreateBuilder(workspace);
        Assert.Equal(JsonValueKind.Object, builder2.RootElement.ValueKind);
    }

    #endregion

    #region Utf8JsonWriter Guid Minimized Grow (lines 93-95)

    /// <summary>
    /// Writing many Guid values triggers the buffer grow path in minimized mode.
    /// Target: Utf8JsonWriter.WriteValues.Guid.cs lines 93-95.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteGuidValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        // MaximumFormatGuidLength=36 + 2 quotes + 1 sep = 39 maxRequired.
        // Initial buffer 256 bytes, need BytesPending > 256-39=217.
        // Each guid ~38 bytes + comma. Need ~6 guids.
        Guid testGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        for (int i = 0; i < 10; i++)
        {
            writer.WriteStringValue(testGuid);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion

    #region Utf8JsonWriter Decimal Minimized Grow (lines 88-90)

    /// <summary>
    /// Writing many decimal values triggers the buffer grow path in minimized mode.
    /// Target: Utf8JsonWriter.WriteValues.Decimal.cs lines 88-90.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteDecimalValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        // MaximumFormatDecimalLength=31 + 1 sep = 32 maxRequired.
        // Need BytesPending > 256-32=224. Each decimal ~20 bytes + comma. Need ~11 decimals.
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumberValue(12345678901234567890.123456789m);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion

    #region Utf8JsonWriter DateTime Minimized Grow (lines 98-100)

    /// <summary>
    /// Writing many DateTime values triggers the buffer grow path in minimized mode.
    /// Target: Utf8JsonWriter.WriteValues.DateTime.cs lines 98-100.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteDateTimeValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        // MaximumDateTimeLength=33 + 2 quotes + 1 sep = 36 maxRequired.
        // Need BytesPending > 256-36=220. Each DateTime ~29 bytes + comma. Need ~8 values.
        DateTime testDate = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        for (int i = 0; i < 12; i++)
        {
            writer.WriteStringValue(testDate);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion

    #region Utf8JsonWriter DateTimeOffset Minimized Grow (lines 98-100)

    /// <summary>
    /// Writing many DateTimeOffset values triggers the buffer grow path in minimized mode.
    /// Target: Utf8JsonWriter.WriteValues.DateTimeOffset.cs lines 98-100.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteDateTimeOffsetValue_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        DateTimeOffset testDate = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(5));
        for (int i = 0; i < 12; i++)
        {
            writer.WriteStringValue(testDate);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
    }

    #endregion

    #region Utf8JsonWriter Bytes list separator in minimized (lines 135-136)

    /// <summary>
    /// Writing two Base64 values in a minimized array triggers the list separator path.
    /// Target: Utf8JsonWriter.WriteValues.Bytes.cs lines 135-136 (list separator in minimized).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonWriter_WriteBase64_ListSeparator()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteBase64StringValue(new byte[] { 1, 2, 3 });
        writer.WriteBase64StringValue(new byte[] { 4, 5, 6 });
        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.Equal("""["AQID","BAUG"]""", result);
    }

    #endregion
}
