// <copyright file="CoverageBatch8Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 8: targeting Utf8JsonWriter property and value write paths
/// that require buffer Grow or list separators.
/// </summary>
public static class CoverageBatch8Tests
{
    #region WritePropertyNameMinimized byte[] — Grow path (Helpers.cs lines 184-186)

    /// <summary>
    /// Exercises <c>WritePropertyNameMinimized(ReadOnlySpan&lt;byte&gt;, byte token)</c> Grow path
    /// by deeply nesting StartObject calls to consume the initial 256-byte buffer.
    /// The method is only called for START tokens (nested objects/arrays).
    /// </summary>
    [Fact]
    public static void Writer_MinimizedStartObject_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();

        // Each nested WriteStartObject("a"u8) writes "a":{ = 5 bytes via WritePropertyNameMinimized.
        // After ~50 nesting levels: BytesPending ≈ 251, remaining ≈ 5.
        // The 51st call needs maxRequired=6, triggers Grow at Helpers.cs lines 184-186.
        for (int i = 0; i < 55; i++)
        {
            writer.WriteStartObject("a"u8);
        }

        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.StartsWith("{\"a\":{", json);
    }

    #endregion

    #region WritePropertyNameMinimized char[] — Grow path (Helpers.cs lines 214-216)

    /// <summary>
    /// Exercises <c>WritePropertyNameMinimized(ReadOnlySpan&lt;char&gt;, byte token)</c> Grow path
    /// via deeply nested StartObject calls with char-based property name.
    /// </summary>
    [Fact]
    public static void Writer_MinimizedStartObjectChar_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();

        // Each WriteStartObject("b".AsSpan()) goes through char overload.
        // maxRequired = (1 * 3) + 5 = 8 bytes. Each writes 5 bytes ("b":{).
        // Need 50+ nestings to consume 256 bytes: 1 + 50*5 = 251, remaining=5 < 8 → Grow.
        for (int i = 0; i < 55; i++)
        {
            writer.WriteStartObject("b".AsSpan());
        }

        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.StartsWith("{\"b\":{", json);
    }

    #endregion

    #region WritePropertyNameIndented — Grow path (Helpers.cs lines 99-101)

    /// <summary>
    /// Exercises <c>WritePropertyNameIndented(ReadOnlySpan&lt;byte&gt;, byte token)</c> Grow path.
    /// Indented mode adds indent bytes per level, so fewer nestings needed.
    /// </summary>
    [Fact]
    public static void Writer_IndentedStartObject_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { Indented = true, SkipValidation = true });

        writer.WriteStartObject();

        // Indented mode: each level adds indent(2*depth) + newline(2) + "c":{ (5 bytes).
        // At depth 5: indent=10, total per level ≈ 17 bytes. Fills 256 quickly.
        for (int i = 0; i < 15; i++)
        {
            writer.WriteStartObject("c"u8);
        }

        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Contains("\"c\":", json);
    }

    #endregion

    #region WriteStringValueMinimized DateTime — list separator (lines 97-100)

    /// <summary>
    /// Exercises <c>WriteStringValueMinimized</c> for DateTime with list separator
    /// (_currentDepth &lt; 0 triggers comma for second element).
    /// </summary>
    [Fact]
    public static void Writer_DateTimeValue_SecondInArray_WritesSeparator()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        DateTime dt1 = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        DateTime dt2 = new(2024, 6, 20, 14, 45, 0, DateTimeKind.Utc);

        writer.WriteStartArray();
        writer.WriteStringValue(dt1);
        writer.WriteStringValue(dt2); // Second element — triggers comma
        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("""["2024-01-15T10:30:00Z","2024-06-20T14:45:00Z"]""", json);
    }

    #endregion

    #region WriteStringValueMinimized DateTimeOffset — list separator

    /// <summary>
    /// Exercises <c>WriteStringValueMinimized</c> for DateTimeOffset with list separator.
    /// </summary>
    [Fact]
    public static void Writer_DateTimeOffsetValue_SecondInArray_WritesSeparator()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        DateTimeOffset dto1 = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        DateTimeOffset dto2 = new(2024, 6, 20, 14, 45, 0, TimeSpan.FromHours(2));

        writer.WriteStartArray();
        writer.WriteStringValue(dto1);
        writer.WriteStringValue(dto2); // Second element — triggers comma
        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("""["2024-01-15T10:30:00+00:00","2024-06-20T14:45:00+02:00"]""", json);
    }

    #endregion

    #region WriteStringValueIndented DateTime — Grow (lines 55-58)

    /// <summary>
    /// Exercises <c>WriteStringValueIndented</c> for DateTime with Grow.
    /// Writes enough DateTime values to consume the initial buffer, triggering Grow.
    /// </summary>
    [Fact]
    public static void Writer_IndentedDateTimeValue_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();

        // Each indented DateTime value is ~30+ bytes (indent + newline + quotes + formatted date).
        // After ~8 values the 256-byte initial buffer is consumed, triggering Grow on the next.
        for (int i = 0; i < 12; i++)
        {
            writer.WriteStringValue(new DateTime(2024, 1, 1 + i, 10, 30, 0, DateTimeKind.Utc));
        }

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Contains("2024-01-12", json);
    }

    #endregion

    #region WriteStringValueIndented DateTimeOffset — Grow (lines 55-58)

    /// <summary>
    /// Exercises <c>WriteStringValueIndented</c> for DateTimeOffset with Grow.
    /// </summary>
    [Fact]
    public static void Writer_IndentedDateTimeOffsetValue_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();

        for (int i = 0; i < 12; i++)
        {
            writer.WriteStringValue(new DateTimeOffset(2024, 1, 1 + i, 10, 30, 0, TimeSpan.FromHours(5)));
        }

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Contains("2024-01-12", json);
    }

    #endregion

    #region WriteBase64 minimized — list separator (lines 134-136)

    /// <summary>
    /// Exercises <c>WriteBase64Minimized</c> comma path (line 134-136: _currentDepth &lt; 0).
    /// </summary>
    [Fact]
    public static void Writer_Base64Value_SecondInArray_WritesSeparator()
    {
        using PooledByteBufferWriter bufferWriter = new(256);
        using Utf8JsonWriter writer = new(bufferWriter);

        byte[] data1 = [1, 2, 3, 4];
        byte[] data2 = [5, 6, 7, 8];

        writer.WriteStartArray();
        writer.WriteBase64StringValue(data1.AsSpan());
        writer.WriteBase64StringValue(data2.AsSpan()); // Second — comma
        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Equal("""["AQIDBA==","BQYHCA=="]""", json);
    }

    #endregion

    #region WriteBase64 indented — Grow (lines 77-79)

    /// <summary>
    /// Exercises <c>WriteBase64Indented</c> Grow path by writing enough base64 values
    /// to consume the initial buffer.
    /// </summary>
    [Fact]
    public static void Writer_IndentedBase64Value_TriggersGrow()
    {
        using PooledByteBufferWriter bufferWriter = new(16);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions { Indented = true });

        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        writer.WriteStartArray();

        // Each base64 value of 16 bytes → 24 chars encoded + indent + newline ≈ 30 bytes.
        // Write enough to consume the initial 256-byte buffer.
        for (int i = 0; i < 12; i++)
        {
            writer.WriteBase64StringValue(data.AsSpan());
        }

        writer.WriteEndArray();
        writer.Flush();

        string json = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.Contains("AQIDBAUGBwgJCgsMDQ4PEA==", json);
    }

    #endregion
}
