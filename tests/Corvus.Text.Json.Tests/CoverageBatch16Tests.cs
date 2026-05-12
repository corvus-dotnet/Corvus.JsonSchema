// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 16: Utf8JsonWriter indented Grow paths for DateTime/DateTimeOffset/Guid,
/// and WriteProperties Grow paths for Decimal/Bytes/DateTime/DateTimeOffset (char and byte overloads).
/// </summary>
[TestClass]
public class CoverageBatch16Tests
{
    #region Indented DateTime Grow (line 58)

    /// <summary>
    /// Writing DateTime values in indented mode triggers the indented Grow path.
    /// Target: Utf8JsonWriter.WriteValues.DateTime.cs line 58 (indented Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeIndented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        writer.WriteStartArray();
        DateTime testDate = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        for (int i = 0; i < 10; i++)
        {
            writer.WriteStringValue(testDate);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "2024-06-15");
    }

    #endregion

    #region Indented DateTimeOffset Grow (line 58)

    /// <summary>
    /// Writing DateTimeOffset values in indented mode triggers the indented Grow path.
    /// Target: Utf8JsonWriter.WriteValues.DateTimeOffset.cs line 58 (indented Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeOffsetIndented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        writer.WriteStartArray();
        DateTimeOffset testDate = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(5));
        for (int i = 0; i < 10; i++)
        {
            writer.WriteStringValue(testDate);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "2024-06-15");
    }

    #endregion

    #region Indented Guid Grow (line 59)

    /// <summary>
    /// Writing Guid values in indented mode triggers the indented Grow path.
    /// Target: Utf8JsonWriter.WriteValues.Guid.cs line 59 (indented Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteGuidIndented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        writer.WriteStartArray();
        Guid testGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        for (int i = 0; i < 8; i++)
        {
            writer.WriteStringValue(testGuid);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "12345678-1234-1234-1234-123456789abc");
    }

    #endregion

    #region WriteProperties.Decimal char Grow (lines 319-321)

    /// <summary>
    /// Writing many string-named decimal properties triggers the char transcoding Grow path.
    /// Target: Utf8JsonWriter.WriteProperties.Decimal.cs lines 319-321 (char overload Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDecimalProperty_CharName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"decimal_property_{i}", 123456789.987654321m);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "decimal_property_0");
    }

    #endregion

    #region WriteProperties.Decimal byte Grow (lines 350-352)

    /// <summary>
    /// Writing many UTF-8 byte-named decimal properties triggers the byte overload Grow path.
    /// Target: Utf8JsonWriter.WriteProperties.Decimal.cs lines 350-352 (byte overload Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDecimalProperty_ByteName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        // Use pre-encoded names to hit the byte[] overload
        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"byte_prop_{i}");
        }

        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], 987654321.123456789m);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "byte_prop_0");
    }

    #endregion

    #region WriteProperties.Bytes char Grow (lines 324-326)

    /// <summary>
    /// Writing many string-named base64 properties triggers the char transcoding Grow path.
    /// Target: Utf8JsonWriter.WriteProperties.Bytes.cs lines 324-326 (char overload Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteBase64Property_CharName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        byte[] testBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteBase64String($"base64_property_{i}", testBytes);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "base64_property_0");
    }

    #endregion

    #region WriteProperties.DateTime char Grow (lines 325-327)

    /// <summary>
    /// Writing many string-named DateTime properties triggers the char transcoding Grow path.
    /// Target: Utf8JsonWriter.WriteProperties.DateTime.cs lines 325-327 (char overload Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeProperty_CharName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTime testDate = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString($"datetime_prop_{i}", testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "datetime_prop_0");
    }

    #endregion

    #region WriteProperties.DateTimeOffset char Grow (lines 324-326)

    /// <summary>
    /// Writing many string-named DateTimeOffset properties triggers the char transcoding Grow path.
    /// Target: Utf8JsonWriter.WriteProperties.DateTimeOffset.cs lines 324-326 (char overload Grow).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeOffsetProperty_CharName_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTimeOffset testDate = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(-5));
        writer.WriteStartObject();
        for (int i = 0; i < 12; i++)
        {
            writer.WriteString($"dtoffset_prop_{i}", testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dtoffset_prop_0");
    }

    #endregion
}
