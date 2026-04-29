// <copyright file="SourceAndNumericCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Numerics;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting:
/// - JsonElement.Source.WriteTo(Utf8JsonWriter) for all Source kinds
/// - JsonElement.Mutable numeric Get* FormatException paths
/// </summary>
public static class SourceAndNumericCoverageTests
{
    #region Source.WriteTo — exercises each Kind branch

    [Fact]
    public static void WriteTo_Null_WritesJsonNull()
    {
        JsonElement.Source source = JsonElement.Source.Null();
        string result = WriteSourceToString(source);
        Assert.Equal("null", result);
    }

    [Fact]
    public static void WriteTo_True_WritesJsonTrue()
    {
        JsonElement.Source source = true;
        string result = WriteSourceToString(source);
        Assert.Equal("true", result);
    }

    [Fact]
    public static void WriteTo_False_WritesJsonFalse()
    {
        JsonElement.Source source = false;
        string result = WriteSourceToString(source);
        Assert.Equal("false", result);
    }

    [Fact]
    public static void WriteTo_NumericSimpleType_Int_WritesNumber()
    {
        JsonElement.Source source = 42;
        string result = WriteSourceToString(source);
        Assert.Equal("42", result);
    }

    [Fact]
    public static void WriteTo_NumericSimpleType_Long_WritesNumber()
    {
        JsonElement.Source source = 9876543210L;
        string result = WriteSourceToString(source);
        Assert.Equal("9876543210", result);
    }

    [Fact]
    public static void WriteTo_NumericSimpleType_Double_WritesNumber()
    {
        JsonElement.Source source = 3.14;
        string result = WriteSourceToString(source);
        Assert.Contains("3.14", result);
    }

    [Fact]
    public static void WriteTo_FormattedNumber_WritesRawNumber()
    {
        JsonElement.Source source = JsonElement.Source.FormattedNumber("1.23e+10"u8);
        string result = WriteSourceToString(source);
        Assert.Equal("1.23e+10", result);
    }

    [Fact]
    public static void WriteTo_Utf16String_WritesQuotedString()
    {
        JsonElement.Source source = "hello world";
        string result = WriteSourceToString(source);
        Assert.Equal("\"hello world\"", result);
    }

    [Fact]
    public static void WriteTo_RawUtf8String_RequiresUnescaping_WritesString()
    {
        JsonElement.Source source = JsonElement.Source.RawString("raw\\nvalue"u8, requiresUnescaping: true);
        string result = WriteSourceToString(source);
        Assert.Contains("raw", result);
    }

    [Fact]
    public static void WriteTo_RawUtf8String_NotRequiresUnescaping_WritesString()
    {
        JsonElement.Source source = JsonElement.Source.RawString("simple"u8, requiresUnescaping: false);
        string result = WriteSourceToString(source);
        Assert.Equal("\"simple\"", result);
    }

    [Fact]
    public static void WriteTo_Utf8String_WritesString()
    {
        // ReadOnlySpan<byte> implicit operator → Kind.Utf8String
        JsonElement.Source source = "utf8test"u8;
        string result = WriteSourceToString(source);
        Assert.Equal("\"utf8test\"", result);
    }

    [Fact]
    public static void WriteTo_JsonElement_WritesElement()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement.Source source = doc.RootElement;
        string result = WriteSourceToString(source);
        Assert.Contains("\"a\"", result);
    }

    [Fact]
    public static void WriteTo_ObjectBuilder_ThrowsInvalidOperationException()
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

        Assert.True(threw);
    }

    [Fact]
    public static void WriteTo_ArrayBuilder_ThrowsInvalidOperationException()
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

        Assert.True(threw);
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

    [Fact]
    public static void GetBigInteger_NonInteger_ThrowsFormatException()
    {
        // 3.14 is not an integer
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("3.14");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => builder.RootElement.GetBigInteger());
    }

#if NET9_0_OR_GREATER
    [Fact]
    public static void GetInt128_OverflowNumber_ThrowsFormatException()
    {
        // Number that exceeds Int128.MaxValue (170141183460469231731687303715884105727, 39 digits)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            "999999999999999999999999999999999999999999");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => builder.RootElement.GetInt128());
    }

    [Fact]
    public static void GetUInt128_NegativeNumber_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("-1");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => builder.RootElement.GetUInt128());
    }

    // GetHalf FormatException path (line 3738-3739) is unreachable — see note above.
#endif

    #endregion

    #region Additional Source kind coverage via SetProperty (exercises AddAsProperty paths)

    [Fact]
    public static void SetProperty_NullSource_SetsPropertyToNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.Null());
        Assert.Equal(JsonValueKind.Null, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    [Fact]
    public static void SetProperty_BoolTrue_SetsPropertyToTrue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source trueSource = true;
        builder.RootElement.SetProperty("x"u8, trueSource);
        Assert.True(builder.RootElement.GetProperty("x"u8).GetBoolean());
    }

    [Fact]
    public static void SetProperty_BoolFalse_SetsPropertyToFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source falseSource = false;
        builder.RootElement.SetProperty("x"u8, falseSource);
        Assert.False(builder.RootElement.GetProperty("x"u8).GetBoolean());
    }

    [Fact]
    public static void SetProperty_FormattedNumber_SetsPropertyToNumber()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.FormattedNumber("99.5"u8));
        Assert.Equal(99.5, builder.RootElement.GetProperty("x"u8).GetDouble());
    }

    [Fact]
    public static void SetProperty_RawUtf8String_RequiresUnescaping_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.RawString("hello\\nworld"u8, requiresUnescaping: true));
        Assert.Equal(JsonValueKind.String, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    [Fact]
    public static void SetProperty_RawUtf8String_NotRequiresUnescaping_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, JsonElement.Source.RawString("plain"u8, requiresUnescaping: false));
        Assert.Equal("plain", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [Fact]
    public static void SetProperty_Utf8String_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source utf8Source = "utf8value"u8;
        builder.RootElement.SetProperty("x"u8, utf8Source);
        Assert.Equal("utf8value", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [Fact]
    public static void SetProperty_Utf16String_SetsProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.SetProperty("x"u8, "utf16 value");
        Assert.Equal("utf16 value", builder.RootElement.GetProperty("x"u8).GetString());
    }

    [Fact]
    public static void SetProperty_JsonElement_SetsPropertyFromElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}""");
        using ParsedJsonDocument<JsonElement> other = ParsedJsonDocument<JsonElement>.Parse("""[10,20]""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source elementSource = other.RootElement;
        builder.RootElement.SetProperty("x"u8, elementSource);
        Assert.Equal(JsonValueKind.Array, builder.RootElement.GetProperty("x"u8).ValueKind);
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
