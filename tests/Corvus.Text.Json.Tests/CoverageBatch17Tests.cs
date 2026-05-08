// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 17: Utf8JsonWriter WriteProperties byte-overload Grow paths
/// for Bytes, DateTime, DateTimeOffset, and indented Bytes/Decimal Grow.
/// </summary>
[TestClass]
public class CoverageBatch17Tests
{
    #region WriteProperties.Bytes byte overload Grow (lines 360-362)

    /// <summary>
    /// Writing many pre-encoded property names with base64 values triggers byte overload Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Bytes.cs lines 360-362.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteBase64Property_ByteName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        byte[] testBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        JsonEncodedText[] names = new JsonEncodedText[12];
        for (int i = 0; i < 12; i++)
        {
            names[i] = JsonEncodedText.Encode($"b64_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteBase64String(names[i], testBytes);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "b64_prop_0");
    }

    #endregion

    #region WriteProperties.DateTime byte overload Grow (lines 359-361)

    /// <summary>
    /// Writing many pre-encoded property names with DateTime values triggers byte overload Grow.
    /// Target: Utf8JsonWriter.WriteProperties.DateTime.cs lines 359-361.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeProperty_ByteName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTime testDate = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        JsonEncodedText[] names = new JsonEncodedText[12];
        for (int i = 0; i < 12; i++)
        {
            names[i] = JsonEncodedText.Encode($"dt_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString(names[i], testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dt_prop_0");
    }

    #endregion

    #region WriteProperties.DateTimeOffset byte overload Grow (lines 358-360)

    /// <summary>
    /// Writing many pre-encoded property names with DateTimeOffset values triggers byte overload Grow.
    /// Target: Utf8JsonWriter.WriteProperties.DateTimeOffset.cs lines 358-360.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeOffsetProperty_ByteName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTimeOffset testDate = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(-5));
        JsonEncodedText[] names = new JsonEncodedText[12];
        for (int i = 0; i < 12; i++)
        {
            names[i] = JsonEncodedText.Encode($"dto_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString(names[i], testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dto_prop_0");
    }

    #endregion

    #region Indented Bytes Grow (line 68-69 — size check) and WriteValues.Bytes Minimized Grow

    /// <summary>
    /// Writing Base64 values in indented mode triggers the indented Grow path.
    /// Target: Utf8JsonWriter.WriteValues.Bytes.cs lines 77-79 (indented Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteBase64Indented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        byte[] testBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        writer.WriteStartArray();
        for (int i = 0; i < 10; i++)
        {
            writer.WriteBase64StringValue(testBytes);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
    }

    /// <summary>
    /// Writing Base64 values in minimized mode triggers the minimized Grow path.
    /// Target: Utf8JsonWriter.WriteValues.Bytes.cs lines 127-129 (minimized Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteBase64Minimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        byte[] testBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        writer.WriteStartArray();

        // Each base64 value is ~27 bytes. ArrayBufferWriter(16) grows to ~272 on first GetMemory(256).
        // Need 11+ elements to exhaust the 272-byte buffer and trigger Grow at line 127-129.
        for (int i = 0; i < 15; i++)
        {
            writer.WriteBase64StringValue(testBytes);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
    }

    #endregion

    #region Indented Decimal Grow (WriteValues.Decimal.cs lines 76-78)

    /// <summary>
    /// Writing decimal values in indented mode triggers the indented Grow path.
    /// Target: Utf8JsonWriter.WriteValues.Decimal.cs lines 76-78 (indented Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDecimalIndented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        writer.WriteStartArray();
        for (int i = 0; i < 10; i++)
        {
            writer.WriteNumberValue(12345678901234567890.123456789m);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
    }

    #endregion

    #region Dead code documentation

    // WriteValues.String.cs lines 116-122: WriteNumberValueAsStringUnescaped — DEAD CODE
    // This internal method has zero callers anywhere in the codebase. It was likely intended
    // for a "write number as string" feature that was never wired up.
    //
    // WriteValues.StringSegment.cs lines 507-508, 565-566: OperationStatus.DestinationTooSmall
    // default case in switch — DEAD CODE. These are Debug.Fail() paths that indicate
    // impossible states. The Utf8/Utf16 conversion operations never return DestinationTooSmall
    // because the output buffer is sized to the maximum expansion.
    //
    // WriteValues.Bytes.cs lines 68-69, 115-116: Value too large overflow checks.
    // These require bytes.Length > int.MaxValue / 4 * 3 (~1.6 GB), which is impractical
    // to test. Documented as IMPRACTICAL rather than dead code since the path is theoretically
    // reachable on systems with sufficient memory.

    #endregion
}
