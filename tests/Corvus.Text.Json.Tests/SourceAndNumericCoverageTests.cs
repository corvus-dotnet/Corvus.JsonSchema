// <copyright file="SourceAndNumericCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Numerics;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting:
/// - JsonElement.Source.WriteTo(Utf8JsonWriter) for all Source kinds
/// - JsonElement.Mutable numeric Get* FormatException paths
/// </summary>
[TestClass]
public class SourceAndNumericCoverageTests
{
    #region Source.WriteTo — exercises each Kind branch

    [TestMethod]
    public void WriteTo_Null_WritesJsonNull()
    {
        JsonElement.Source source = JsonElement.Source.Null();
        string result = WriteSourceToString(source);
        Assert.AreEqual("null", result);
    }

    [TestMethod]
    public void WriteTo_True_WritesJsonTrue()
    {
        JsonElement.Source source = true;
        string result = WriteSourceToString(source);
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void WriteTo_False_WritesJsonFalse()
    {
        JsonElement.Source source = false;
        string result = WriteSourceToString(source);
        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void WriteTo_NumericSimpleType_Int_WritesNumber()
    {
        JsonElement.Source source = 42;
        string result = WriteSourceToString(source);
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void WriteTo_NumericSimpleType_Long_WritesNumber()
    {
        JsonElement.Source source = 9876543210L;
        string result = WriteSourceToString(source);
        Assert.AreEqual("9876543210", result);
    }

    [TestMethod]
    public void WriteTo_NumericSimpleType_Double_WritesNumber()
    {
        JsonElement.Source source = 3.14;
        string result = WriteSourceToString(source);
        StringAssert.Contains(result, "3.14");
    }

    [TestMethod]
    public void WriteTo_FormattedNumber_WritesRawNumber()
    {
        JsonElement.Source source = JsonElement.Source.FormattedNumber("1.23e+10"u8);
        string result = WriteSourceToString(source);
        Assert.AreEqual("1.23e+10", result);
    }

    [TestMethod]
    public void WriteTo_StringSimpleType_Guid_WritesQuotedString()
    {
        Guid g = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        JsonElement.Source source = g;
        string result = WriteSourceToString(source);
        StringAssert.Contains(result, "01234567-89ab-cdef-0123-456789abcdef");
    }

    [TestMethod]
    public void WriteTo_Utf16String_WritesQuotedString()
    {
        JsonElement.Source source = "hello world";
        string result = WriteSourceToString(source);
        Assert.AreEqual("\"hello world\"", result);
    }

    [TestMethod]
    public void WriteTo_RawUtf8String_RequiresUnescaping_WritesString()
    {
        JsonElement.Source source = JsonElement.Source.RawString("raw\\nvalue"u8, requiresUnescaping: true);
        string result = WriteSourceToString(source);
        StringAssert.Contains(result, "raw");
    }

    [TestMethod]
    public void WriteTo_RawUtf8String_NotRequiresUnescaping_WritesString()
    {
        JsonElement.Source source = JsonElement.Source.RawString("simple"u8, requiresUnescaping: false);
        string result = WriteSourceToString(source);
        Assert.AreEqual("\"simple\"", result);
    }

    [TestMethod]
    public void WriteTo_Utf8String_WritesString()
    {
        // ReadOnlySpan<byte> implicit operator → Kind.Utf8String
        JsonElement.Source source = "utf8test"u8;
        string result = WriteSourceToString(source);
        Assert.AreEqual("\"utf8test\"", result);
    }

    [TestMethod]
    public void WriteTo_JsonElement_WritesElement()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement.Source source = doc.RootElement;
        string result = WriteSourceToString(source);
        StringAssert.Contains(result, "\"a\"");
    }

    [TestMethod]
    public void WriteTo_ObjectBuilder_ThrowsInvalidOperationException()
    {
        JsonElement.Source source = new(
            static (ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("x"u8, 1);
            });

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        bool threw = false;
        try
        {
            source.WriteTo(writer);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void WriteTo_ArrayBuilder_ThrowsInvalidOperationException()
    {
        JsonElement.Source source = new(
            static (ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(1);
            });

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        bool threw = false;
        try
        {
            source.WriteTo(writer);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    #endregion

    #region Mutable numeric Get* FormatException paths

    // NOTE: GetDouble, GetSingle, and GetHalf FormatException paths (lines 3459-3460,
    // 3526-3527, 3738-3739) are unreachable through normal use. The mutable builder's
    // TryGetDouble/TryGetSingle/TryGetHalf never return false for valid JSON numbers —
    // they accept overflow as Infinity rather than rejecting the value. Verified by
    // testing with 1e309 (which exceeds double.MaxValue): all three methods succeed.
    // These paths exist as defensive code against future implementation changes.
    // Similarly, GetBigNumber's FormatException path (lines 3794-3795) is unreachable
    // because BigNumber can parse any valid JSON number up to 10,000 characters.

    [TestMethod]
    public void GetBigInteger_NonInteger_ThrowsFormatException()
    {
        // 3.14 is not an integer
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("3.14");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.ThrowsExactly<FormatException>(() => builder.RootElement.GetBigInteger());
    }

#if NET9_0_OR_GREATER
    [TestMethod]
    public void GetInt128_OverflowNumber_ThrowsFormatException()
    {
        // Number that exceeds Int128.MaxValue (170141183460469231731687303715884105727, 39 digits)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            "999999999999999999999999999999999999999999");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.ThrowsExactly<FormatException>(() => builder.RootElement.GetInt128());
    }

    [TestMethod]
    public void GetUInt128_NegativeNumber_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("-1");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.ThrowsExactly<FormatException>(() => builder.RootElement.GetUInt128());
    }

    // GetHalf FormatException path (line 3738-3739) is unreachable — see note above.
#endif

    #endregion

    #region Additional Source kind coverage via SetProperty (exercises AddAsProperty paths)

    [TestMethod]
    public void SetProperty_NullSource_SetsPropertyToNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.Null());
        Assert.AreEqual(JsonValueKind.Null, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    [TestMethod]
    public void SetProperty_BoolTrue_SetsPropertyToTrue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source trueSource = true;
        builder.RootElement.SetProperty("x"u8, trueSource);
        Assert.IsTrue(builder.RootElement.GetProperty("x"u8).GetBoolean());
    }

    [TestMethod]
    public void SetProperty_BoolFalse_SetsPropertyToFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source falseSource = false;
        builder.RootElement.SetProperty("x"u8, falseSource);
        Assert.IsFalse(builder.RootElement.GetProperty("x"u8).GetBoolean());
    }

    [TestMethod]
    public void SetProperty_FormattedNumber_SetsPropertyToNumber()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.FormattedNumber("99.5"u8));
        Assert.AreEqual(99.5, builder.RootElement.GetProperty("x"u8).GetDouble());
    }

    [TestMethod]
    public void SetProperty_RawUtf8String_RequiresUnescaping_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.RawString("hello\\nworld"u8, requiresUnescaping: true));
        Assert.AreEqual(JsonValueKind.String, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    [TestMethod]
    public void SetProperty_RawUtf8String_NotRequiresUnescaping_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.RawString("plain"u8, requiresUnescaping: false));
        Assert.AreEqual("plain", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [TestMethod]
    public void SetProperty_Utf8String_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source utf8Source = "utf8value"u8;
        builder.RootElement.SetProperty("x"u8, utf8Source);
        Assert.AreEqual("utf8value", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [TestMethod]
    public void SetProperty_Utf16String_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, "utf16 value");
        Assert.AreEqual("utf16 value", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [TestMethod]
    public void SetProperty_JsonElement_SetsPropertyFromElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using ParsedJsonDocument<JsonElement> other = ParsedJsonDocument<JsonElement>.Parse("""[10,20]""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source elementSource = other.RootElement;
        builder.RootElement.SetProperty("x"u8, elementSource);
        Assert.AreEqual(JsonValueKind.Array, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    #endregion

    #region Helpers

    private static string WriteSourceToString(JsonElement.Source source)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        source.WriteTo(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    #endregion
}
