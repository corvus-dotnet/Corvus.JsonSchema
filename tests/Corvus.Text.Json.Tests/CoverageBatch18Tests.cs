// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 18: WriteProperties char-overload minimized Grow paths,
/// WriteProperties byte-overload minimized Grow for remaining types,
/// WriteProperties.Helpers all 3 Grow paths, and WriteValues.Double minimized Grow.
/// </summary>
[TestClass]
public class CoverageBatch18Tests
{
    #region WriteValues.Double minimized Grow (lines 57-59)

    /// <summary>
    /// Writing many doubles in minimized array mode triggers minimized Grow.
    /// Target: Utf8JsonWriter.WriteValues.Double.cs lines 57-59.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDoubleMinimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();

        // MaximumFormatDoubleLength + 1 = 129; buffer ~272 after first Grow.
        // Each double is ~5-20 bytes. Need 15+ doubles to exhaust buffer.
        for (int i = 0; i < 30; i++)
        {
            writer.WriteNumberValue(1.23456789012345 + i);
        }

        writer.WriteEndArray();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        Assert.StartsWith("[", result);
    }

    #endregion

    #region WriteProperties.Helpers Grow paths (lines 99-102, 184-187, 214-217)

    /// <summary>
    /// Triggers WritePropertyNameIndented(ReadOnlySpan&lt;byte&gt;, token) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Helpers.cs lines 99-102.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteStartObjectByteIndented_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true, Indented = true });

        JsonEncodedText[] names = new JsonEncodedText[20];
        for (int i = 0; i < 20; i++)
        {
            names[i] = JsonEncodedText.Encode($"object_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 20; i++)
        {
            writer.WriteStartObject(names[i]);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "object_prop_0");
    }

    /// <summary>
    /// Triggers WritePropertyNameMinimized(ReadOnlySpan&lt;byte&gt;, token) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Helpers.cs lines 184-187.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteStartObjectByteMinimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[20];
        for (int i = 0; i < 20; i++)
        {
            names[i] = JsonEncodedText.Encode($"object_prop_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 20; i++)
        {
            writer.WriteStartObject(names[i]);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "object_prop_0");
    }

    /// <summary>
    /// Triggers WritePropertyNameMinimized(ReadOnlySpan&lt;char&gt;, token) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Helpers.cs lines 214-217.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteStartObjectCharMinimized_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 20; i++)
        {
            writer.WriteStartObject($"object_prop_{i}");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "object_prop_0");
    }

    #endregion

    #region WriteProperties char-overload minimized Grow (Decimal, Double, Float, SignedNumber, UnsignedNumber, Guid, Bytes, DateTime, DateTimeOffset)

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;char&gt;, decimal) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Decimal.cs lines 318-321, 326-328.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDecimalPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"decimal_prop_{i}", 1.23456789m);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "decimal_prop_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;char&gt;, double) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Double.cs lines 322-324, 329-331.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDoublePropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"double_prop_{i}", 1.23456789 + i);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "double_prop_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;char&gt;, float) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Float.cs lines 322-324, 329-331.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteFloatPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"float_prop_{i}", 1.23f + i);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "float_prop_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;char&gt;, long) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.SignedNumber.cs lines 391-394, 399-401.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteSignedNumberPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"signed_prop_{i}", (long)(100000 + i));
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "signed_prop_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;char&gt;, ulong) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.UnsignedNumber.cs lines 400-403, 408-410.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteUnsignedNumberPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber($"unsigned_prop_{i}", (ulong)(100000 + i));
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "unsigned_prop_0");
    }

    /// <summary>
    /// Triggers WriteStringMinimized(ReadOnlySpan&lt;char&gt; name, Guid) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Guid.cs lines 327-329, 334-336.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteGuidPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString($"guid_prop_{i}", Guid.Empty);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "guid_prop_0");
    }

    /// <summary>
    /// Triggers WriteBase64Minimized(ReadOnlySpan&lt;char&gt; name, bytes) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Bytes.cs lines 323-326, 331-333.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteBytesPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        byte[] testBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteBase64String($"bytes_prop_{i}", testBytes);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "bytes_prop_0");
    }

    /// <summary>
    /// Triggers WriteStringMinimized(ReadOnlySpan&lt;char&gt; name, DateTime) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.DateTime.cs lines 325-327, 332-334.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimePropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTime testDate = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString($"datetime_prop_{i}", testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "datetime_prop_0");
    }

    /// <summary>
    /// Triggers WriteStringMinimized(ReadOnlySpan&lt;char&gt; name, DateTimeOffset) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.DateTimeOffset.cs lines 324-326, 331-333.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDateTimeOffsetPropertyChar_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        DateTimeOffset testDate = new DateTimeOffset(2024, 6, 15, 12, 30, 45, TimeSpan.Zero);
        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString($"dtoffset_prop_{i}", testDate);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dtoffset_prop_0");
    }

    #endregion

    #region WriteProperties byte-overload minimized Grow (Decimal, Double, Float, Guid, SignedNumber, UnsignedNumber)

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;byte&gt;, decimal) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Decimal.cs lines 349-352.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDecimalPropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"dec_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], 1.23456789m);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dec_byte_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;byte&gt;, double) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Double.cs lines 353-355.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteDoublePropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"dbl_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], 1.23456789 + i);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "dbl_byte_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;byte&gt;, float) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Float.cs lines 353-355.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteFloatPropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"flt_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], 1.23f + i);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "flt_byte_0");
    }

    /// <summary>
    /// Triggers WriteStringMinimized(ReadOnlySpan&lt;byte&gt;, Guid) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.Guid.cs lines 362-364.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteGuidPropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"guid_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteString(names[i], Guid.Empty);
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "guid_byte_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;byte&gt;, long) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.SignedNumber.cs lines 422-425.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteSignedNumberPropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"signed_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], (long)(100000 + i));
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "signed_byte_0");
    }

    /// <summary>
    /// Triggers WriteNumberMinimized(ReadOnlySpan&lt;byte&gt;, ulong) Grow.
    /// Target: Utf8JsonWriter.WriteProperties.UnsignedNumber.cs lines 431-434.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Utf8JsonWriter_WriteUnsignedNumberPropertyByte_TriggersGrow()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 16);
        using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { SkipValidation = true });

        JsonEncodedText[] names = new JsonEncodedText[15];
        for (int i = 0; i < 15; i++)
        {
            names[i] = JsonEncodedText.Encode($"unsigned_byte_{i}");
        }

        writer.WriteStartObject();
        for (int i = 0; i < 15; i++)
        {
            writer.WriteNumber(names[i], (ulong)(100000 + i));
        }

        writer.WriteEndObject();
        writer.Flush();

        string result = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenMemory.ToArray());
        StringAssert.Contains(result, "unsigned_byte_0");
    }

    #endregion
}
