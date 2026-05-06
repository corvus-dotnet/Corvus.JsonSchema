// <copyright file="ObjectBuilderCharSpanArrayCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Xunit;

/// <summary>
/// Coverage tests for ObjectBuilder.AddArrayValue overloads with ReadOnlySpan&lt;char&gt; property names.
/// Targets lines 1325-1466 in JsonElement.ObjectBuilder.cs.
/// </summary>
public static class ObjectBuilderCharSpanArrayCoverageTests
{
    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Long_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<long>)[1L, 2L, 3L]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1,2,3]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Int_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<int>)[10, 20]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[10,20]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Short_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<short>)[1, 2]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1,2]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Sbyte_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<sbyte>)[(sbyte)1, (sbyte)-1]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1,-1]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Ulong_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<ulong>)[100UL, 200UL]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[100,200]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Uint_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<uint>)[5U, 6U]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[5,6]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Ushort_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<ushort>)[(ushort)7, (ushort)8]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[7,8]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Byte_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<byte>)[(byte)0, (byte)255]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[0,255]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Decimal_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            decimal[] arr = [1.5m, 2.5m];
            ob.AddArrayValue("items".AsSpan(), arr.AsSpan());
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1.5,2.5]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Double_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<double>)[1.1, 2.2]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1.1,2.2]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Float_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddArrayValue("items".AsSpan(), (ReadOnlySpan<float>)[3.14f, 2.71f]);
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[3.14,2.71]}""", json);
    }

#if NET

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Int128_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            Int128[] arr = [(Int128)42, (Int128)99];
            ob.AddArrayValue("items".AsSpan(), arr.AsSpan());
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[42,99]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_UInt128_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            UInt128[] arr = [(UInt128)1, (UInt128)2];
            ob.AddArrayValue("items".AsSpan(), arr.AsSpan());
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1,2]}""", json);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void AddArrayValue_Half_CharPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            Half[] arr = [(Half)1.0f, (Half)2.0f];
            ob.AddArrayValue("items".AsSpan(), arr.AsSpan());
        }));

        string json = root["arr"].ToString();
        Assert.Equal("""{"items":[1,2]}""", json);
    }

#endif
}
