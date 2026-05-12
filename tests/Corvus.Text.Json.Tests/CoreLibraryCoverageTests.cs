// <copyright file="CoreLibraryCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.IO.Tests;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for core library gaps in ParsedJsonDocument, JsonDocumentBuilder,
/// and ComplexValueBuilder.
/// </summary>
[TestClass]
public class CoreLibraryCoverageTests
{
    #region Stream parsing — buffer resize (JsonDocumentBuilder.Parse.cs lines 609-617)

    [TestMethod]
    public void ParseFromStream_NonSeekableStream_LargeDocument_TriggersResize()
    {
        // Non-seekable stream gets initial buffer of 4096 bytes (UnseekableStreamInitialRentSize).
        // JSON > 4096 bytes triggers the buffer resize logic at Parse.cs lines 608-617.
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 400; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"item_{i:D4}_padding_to_make_this_longer\"");
        }

        sb.Append(']');

        byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
        Assert.IsTrue(data.Length > 4096, "JSON must exceed 4096 bytes for buffer resize");

        using var stream = new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data);
        using var doc = ParsedJsonDocument<JsonElement>.Parse(stream);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(400, root.GetArrayLength());
    }

    [TestMethod]
    public void ParseBuilderFromStream_NonSeekable_LargeDocument_TriggersResize()
    {
        // Same test but via JsonDocumentBuilder.Parse to cover that parallel path
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 400; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"value_{i:D4}_padding_to_exceed_4096_buffer\"");
        }

        sb.Append(']');

        byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
        Assert.IsTrue(data.Length > 4096, "JSON must exceed 4096 bytes");

        using var workspace = JsonWorkspace.Create();
        using var stream = new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data);
        using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, stream);

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
    }

    #endregion

    #region ComplexValueBuilder.RemoveProperty via ObjectBuilder (lines 3286-3354)

    [TestMethod]
    public void ObjectBuilder_RemoveProperty_LongCharName_TriggersArrayPool()
    {
        // ObjectBuilder.RemoveProperty(string) calls ComplexValueBuilder.RemoveProperty(ReadOnlySpan<char>)
        // which rents from ArrayPool for names where MaxByteCount > 256
        string longName = new string('x', 300);
        byte[] json = """{"existing": 1}"""u8.ToArray();

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty(
            "container"u8,
            longName,
            static (in string name, ref JsonElement.ObjectBuilder b) =>
            {
                b.AddProperty(name, "somevalue");
                b.RemoveProperty(name);
            });

        string result = root.ToString();
        Assert.DoesNotContain(longName, result);
    }

    [TestMethod]
    public void ObjectBuilder_TryApply_WithExistingProperty_TriggersRemove()
    {
        // TryApply iterates source object properties, calling RemoveProperty + AddProperty for each.
        // When existing properties conflict, this triggers ComplexValueBuilder.RemoveProperty(byte[], false, nameIsEscaped)
        byte[] json = """{"base": true}"""u8.ToArray();
        byte[] overlay = """{"name": "new"}"""u8.ToArray();

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Use ParseValue so the element lives without needing disposal tracking
        JsonElement overlayElement = JsonElement.ParseValue(overlay);

        root.SetProperty(
            "container"u8,
            overlayElement,
            static (in JsonElement ov, ref JsonElement.ObjectBuilder b) =>
            {
                b.AddProperty("name"u8, "old"u8);
                b.TryApply(ov);
            });

        string result = root.ToString();
        StringAssert.Contains(result, "\"new\"");
    }

    [TestMethod]
    public void ObjectBuilder_TryApply_WithEscapedPropertyName_TriggersUnescape()
    {
        // An escaped property name in the source document triggers the unescaping path
        // at ComplexValueBuilder.RemoveProperty lines 3331-3354
        byte[] json = """{"base": true}"""u8.ToArray();
        byte[] overlay = """{"hello\nworld": "new"}"""u8.ToArray();

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Use ParseValue so the element lives without needing disposal tracking
        JsonElement overlayElement = JsonElement.ParseValue(overlay);

        root.SetProperty(
            "container"u8,
            overlayElement,
            static (in JsonElement ov, ref JsonElement.ObjectBuilder b) =>
            {
                b.AddProperty("hello\nworld"u8, "old"u8);
                b.TryApply(ov);
            });

        string result = root.ToString();
        StringAssert.Contains(result, "\"new\"");
    }

    #endregion

    #region Mutable.RemoveProperty — empty/long name via JsonElementHelpers

    [TestMethod]
    public void MutableRemoveProperty_EmptyObject_ReturnsFalse()
    {
        byte[] json = """{}"""u8.ToArray();
        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("anything"u8);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void MutableRemoveProperty_LongStringName_TriggersArrayPool()
    {
        // This exercises JsonElementHelpers.RemovePropertyUnsafe with a long char name
        string longName = new string('x', 300);
        string json = $"{{\"{longName}\": 42, \"other\": true}}";

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty(longName);
        Assert.IsTrue(removed);

        string result = root.ToString();
        Assert.DoesNotContain(longName, result);
        StringAssert.Contains(result, "\"other\"");
    }

    #endregion

    #region Numeric formatting — large numbers trigger ArrayPool rental (lines 1002-1005)

    [TestMethod]
    public void TryFormatNumber_LargeDecimalPrecision_TriggersArrayPool()
    {
        // A number with many decimal places (>256 chars total) triggers ArrayPool rental
        var sb = new StringBuilder();
        sb.Append("0.");
        for (int i = 0; i < 300; i++)
        {
            sb.Append((char)('0' + (i % 10)));
        }

        byte[] utf8Number = Encoding.UTF8.GetBytes(sb.ToString());

        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[512];
        bool success = JsonElementHelpers.TryFormatNumber(
            utf8Number,
            destination,
            out int charsWritten,
            "F300".AsSpan(),
            CultureInfo.InvariantCulture,
            isNegative,
            integral,
            fractional,
            exponent);

        Assert.IsTrue(success);
        Assert.IsTrue(charsWritten > 256);
    }

    [TestMethod]
    public void TryFormatNumber_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "1234.5678"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> tinyDest = stackalloc char[1];
        bool success = JsonElementHelpers.TryFormatNumber(
            utf8Number,
            tinyDest,
            out int charsWritten,
            "C".AsSpan(),
            CultureInfo.InvariantCulture,
            isNegative,
            integral,
            fractional,
            exponent);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region Numeric UTF-8 formatting — buffer too small

    [TestMethod]
    public void TryFormatNumberUtf8_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "1234.5678"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> tinyDest = stackalloc byte[1];
        bool success = JsonElementHelpers.TryFormatNumber(
            utf8Number,
            tinyDest,
            out int bytesWritten,
            "C".AsSpan(),
            CultureInfo.InvariantCulture,
            isNegative,
            integral,
            fractional,
            exponent);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatPercentUtf8_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "0.1234"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> tinyDest = stackalloc byte[1];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            tinyDest,
            out int bytesWritten,
            2,
            NumberFormatInfo.InvariantInfo);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatCurrencyUtf8_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "1234.56"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> tinyDest = stackalloc byte[1];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            tinyDest,
            out int bytesWritten,
            2,
            NumberFormatInfo.InvariantInfo);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatGeneral_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "1234.5678"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> tinyDest = stackalloc byte[1];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            tinyDest,
            out int bytesWritten,
            6,
            'E',
            NumberFormatInfo.InvariantInfo);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatFixedPointWithSeparator_BufferTooSmall_ReturnsFalse()
    {
        byte[] utf8Number = "1234567.89"u8.ToArray();
        JsonElementHelpers.ParseNumber(
            utf8Number,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> tinyDest = stackalloc byte[1];
        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            tinyDest,
            out int bytesWritten,
            2,
            NumberFormatInfo.InvariantInfo.NumberDecimalSeparator,
            NumberFormatInfo.InvariantInfo);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion
}
