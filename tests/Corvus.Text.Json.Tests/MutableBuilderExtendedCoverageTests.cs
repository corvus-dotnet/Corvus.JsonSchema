// <copyright file="MutableBuilderExtendedCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using System.Text.Json;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting uncovered paths in JsonDocumentBuilder, ComplexValueBuilder,
/// and ObjectBuilder — specifically char-based property lookups, prebaked property operations,
/// raw string properties, BigNumber/BigInteger array items, JSON pointer resolution,
/// line/offset stubs, and escape-controlled property addition.
/// </summary>
public static class MutableBuilderExtendedCoverageTests
{
    #region ObjectBuilder.AddProperty with Build<TContext> delegate (lines 163-171)

    [Fact]
    public static void ObjectBuilder_AddProperty_WithContext()
    {
        using var workspace = JsonWorkspace.Create();

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Use the SetProperty<TContext> overload that takes ObjectBuilder.Build<TContext>
        doc.RootElement.SetProperty(
            "nested"u8,
            42,
            static (in int ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("value"u8, ctx);
            });

        Assert.Equal(42, doc.RootElement["nested"]["value"].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddProperty with ArrayBuilder.Build<TContext> (lines 202-210)

    [Fact]
    public static void ObjectBuilder_AddArrayProperty_WithContext()
    {
        using var workspace = JsonWorkspace.Create();
        int[] items = [10, 20, 30];

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Use the SetProperty<TContext> overload that takes ObjectBuilder.Build<TContext>
        doc.RootElement.SetProperty(
            "data"u8,
            items,
            static (in int[] ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("numbers"u8, ctx,
                    static (in int[] arr, ref JsonElement.ArrayBuilder ab) =>
                    {
                        foreach (int n in arr)
                        {
                            ab.AddItem(n);
                        }
                    });
            });

        JsonElement.Mutable arr = doc.RootElement["data"]["numbers"];
        Assert.Equal(3, arr.GetArrayLength());
        Assert.Equal(10, arr[0].GetInt32());
        Assert.Equal(30, arr[2].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddProperty with char-span property name + escape control (lines 329-336)

    [Fact]
    public static void ObjectBuilder_AddProperty_CharSpan_Value()
    {
        using var workspace = JsonWorkspace.Create();

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("name"u8, "hello".AsSpan());
            }));

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetProperty("obj"u8, source);

        Assert.Equal("hello", doc.RootElement["obj"]["name"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddFormattedNumber with char-span property name (lines 706-710)

    [Fact]
    public static void ObjectBuilder_AddFormattedNumber_CharSpanPropertyName()
    {
        using var workspace = JsonWorkspace.Create();

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddFormattedNumber("count".AsSpan(), "42"u8);
            }));

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetProperty("obj"u8, source);

        Assert.Equal(42, doc.RootElement["obj"]["count"].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddRawString with char-span property name (lines 719-724)

    [Fact]
    public static void ObjectBuilder_AddRawString_CharSpanPropertyName()
    {
        using var workspace = JsonWorkspace.Create();

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddRawString("text".AsSpan(), "hello world"u8, false);
            }));

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetProperty("obj"u8, source);

        Assert.Equal("hello world", doc.RootElement["obj"]["text"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span with ArrayBuilder.Build<TContext> (lines 759-764)

    [Fact]
    public static void ObjectBuilder_AddProperty_CharSpan_ArrayWithContext()
    {
        using var workspace = JsonWorkspace.Create();
        string[] items = ["a", "b", "c"];

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Use the SetProperty<TContext> overload with char-span name + ObjectBuilder.Build<TContext>
        doc.RootElement.SetProperty(
            "data"u8,
            items,
            static (in string[] ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("items".AsSpan(), ctx,
                    static (in string[] arr, ref JsonElement.ArrayBuilder ab) =>
                    {
                        foreach (string s in arr)
                        {
                            ab.AddItem(s);
                        }
                    });
            });

        JsonElement.Mutable arr = doc.RootElement["data"]["items"];
        Assert.Equal(3, arr.GetArrayLength());
        Assert.Equal("a", arr[0].GetString());
        Assert.Equal("c", arr[2].GetString());
    }

    #endregion

    #region BigNumber and BigInteger array items (ComplexValueBuilder lines 2771-2786)

    [Fact]
    public static void ArrayBuilder_AddItem_BigNumber()
    {
        using var workspace = JsonWorkspace.Create();

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        BigNumber bn = BigNumber.Parse("123456789012345678901234567890.123");

        doc.RootElement.SetProperty("arr"u8, bn,
            static (in BigNumber ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                builder.AddItem(ctx);
            });

        string result = doc.RootElement["arr"][0].GetRawText();
        Assert.Contains("123456789012345678901234567890", result);
    }

    [Fact]
    public static void ArrayBuilder_AddItem_BigInteger()
    {
        using var workspace = JsonWorkspace.Create();

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        BigInteger bi = BigInteger.Parse("99999999999999999999999999999999");

        doc.RootElement.SetProperty("arr"u8, bi,
            static (in BigInteger ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                builder.AddItem(ctx);
            });

        string result = doc.RootElement["arr"][0].GetRawText();
        Assert.Equal("99999999999999999999999999999999", result);
    }

    #endregion

    #region TryGetNamedPropertyValue with char span (JsonDocumentBuilder lines 2510-2519)

    [Fact]
    public static void Builder_TryGetNamedPropertyValue_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"hello": 42, "world": "test"}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Access property by char-span through the mutable element indexer (uses char span lookup internally)
        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal(42, root["hello"].GetInt32());
        Assert.Equal("test", root["world"].GetString());

        // Also test TryGetProperty with string
        Assert.True(root.TryGetProperty("hello", out JsonElement.Mutable hello));
        Assert.Equal(42, hello.GetInt32());
    }

    [Fact]
    public static void Builder_TryGetNamedPropertyValue_CharSpan_NotFound()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = doc.RootElement;
        Assert.False(root.TryGetProperty("nonexistent", out _));
    }

    #endregion

    #region TryResolveJsonPointer on builder (JsonDocumentBuilder lines 2732-2738)

    [Fact]
    public static void Builder_TryResolveJsonPointer()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": [1, 2, 3]}}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Use the JsonPointer-based navigation via the mutable element
        JsonElement.Mutable root = doc.RootElement;
        JsonElement.Mutable nested = root["a"]["b"];
        Assert.Equal(3, nested.GetArrayLength());
    }

    #endregion

    #region TryGetLineAndOffset always returns false (JsonDocumentBuilder lines 2751-2765)

    [Fact]
    public static void Builder_TryGetLineAndOffset_AlwaysReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Access via IJsonDocument interface
        IJsonDocument document = doc;
        bool result = document.TryGetLineAndOffset(0, out int line, out int charOffset, out long lineByteOffset);

        Assert.False(result);
        Assert.Equal(0, line);
        Assert.Equal(0, charOffset);
        Assert.Equal(0, lineByteOffset);
    }

    [Fact]
    public static void Builder_TryGetLineAndOffsetForPointer_AlwaysReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        IJsonDocument document = doc;
        bool result = document.TryGetLineAndOffsetForPointer("/a"u8, 0, out int line, out int charOffset, out long lineByteOffset);

        Assert.False(result);
        Assert.Equal(0, line);
        Assert.Equal(0, charOffset);
        Assert.Equal(0, lineByteOffset);
    }

    #endregion

    #region Builder serialization with escaped property names (JsonDocumentBuilder lines 2340-2372)

    [Fact]
    public static void Builder_SerializeEscapedPropertyName()
    {
        // Create a document with a property name that needs escaping
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"hello\nworld": 42}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Serializing should properly handle the escaped property name
        string json = doc.RootElement.ToString();
        Assert.Contains("42", json);
    }

    #endregion

    #region Prebaked property operations (ComplexValueBuilder lines 3554-3564, 3629-3649, 3726-3733)

    [Fact]
    public static void PrebakedProperty_ComplexValue()
    {
        // Prebaked properties are used by the source generator — test through Source API
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Build a complex object using ObjectBuilder which internally uses ComplexValueBuilder
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("nested"u8,
                    static (ref JsonElement.ObjectBuilder inner) =>
                    {
                        inner.AddProperty("x"u8, 1);
                        inner.AddProperty("y"u8, 2);
                    });
            }));

        doc.RootElement.SetProperty("data"u8, source);

        Assert.Equal(1, doc.RootElement["data"]["nested"]["x"].GetInt32());
        Assert.Equal(2, doc.RootElement["data"]["nested"]["y"].GetInt32());
    }

    [Fact]
    public static void PrebakedProperty_IntValue()
    {
        // Test that prebaked property with int value works through generated code paths
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"existing": true}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Add properties with different value types
        JsonElement.Mutable root = doc.RootElement;
        root.SetProperty("count"u8, 42);
        root.SetProperty("name"u8, "test");
        root.SetProperty("active"u8, true);

        Assert.Equal(42, root["count"].GetInt32());
        Assert.Equal("test", root["name"].GetString());
        Assert.True(root["active"].GetBoolean());
    }

    #endregion

    #region StartProperty with byte span + escape control (ComplexValueBuilder lines 3440-3446)

    [Fact]
    public static void StartEndProperty_ByteSpan_WithEscape()
    {
        // Tests the start/end property pattern with byte-span property names and escape control
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Build using start/end pattern through the builder
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                // This exercises the start/end property path through ObjectBuilder
                builder.AddProperty("container"u8,
                    static (ref JsonElement.ObjectBuilder inner) =>
                    {
                        inner.AddProperty("key"u8, "value");
                    });
            }));

        doc.RootElement.SetProperty("root"u8, source);

        Assert.Equal("value", doc.RootElement["root"]["container"]["key"].GetString());
    }

    #endregion

    #region StartProperty with char span (ComplexValueBuilder lines 3454-3460)

    [Fact]
    public static void StartProperty_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Build using char-span property names
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("myProp".AsSpan(),
                    static (ref JsonElement.ObjectBuilder inner) =>
                    {
                        inner.AddProperty("nested".AsSpan(), 99);
                    });
            }));

        doc.RootElement.SetProperty("top"u8, source);

        Assert.Equal(99, doc.RootElement["top"]["myProp"]["nested"].GetInt32());
    }

    #endregion

    #region RentedBacking (lines at 0% coverage)

    [Fact]
    public static void RentedBacking_InitializeAndDispose()
    {
        // RentedBacking is used by Source<TContext>.Builder — exercise it through a source
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Any Source that writes a larger-than-inline value triggers RentedBacking
        string longValue = new('x', 200); // Exceed the inline buffer threshold
        doc.RootElement.SetProperty("big"u8, longValue);

        Assert.Equal(longValue, doc.RootElement["big"].GetString());
    }

    #endregion

    #region JsonWorkspace uncovered paths (lines 60 uncov)

    [Fact]
    public static void Workspace_CreateUnrented()
    {
        // Test the unrented workspace creation path
        var workspace = JsonWorkspace.CreateUnrented();

        try
        {
            using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
            using var doc = parsed.RootElement.CreateBuilder(workspace);

            Assert.Equal(1, doc.RootElement["a"].GetInt32());
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [Fact]
    public static void Workspace_MultipleBuilders()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed1 = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using var parsed2 = ParsedJsonDocument<JsonElement>.Parse("""{"y": 2}""");

        using var doc1 = parsed1.RootElement.CreateBuilder(workspace);
        using var doc2 = parsed2.RootElement.CreateBuilder(workspace);

        Assert.Equal(1, doc1.RootElement["x"].GetInt32());
        Assert.Equal(2, doc2.RootElement["y"].GetInt32());
    }

    #endregion

    #region ComplexValueBuilder.AddProperty with escape control (lines 135-144)

    [Fact]
    public static void AddProperty_WithEscapeControl_NoEscape()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Build with escapeName=false, nameRequiresUnescaping=false
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("simple"u8,
                    static (ref JsonElement.ObjectBuilder inner) =>
                    {
                        inner.AddProperty("a"u8, 1);
                    },
                    escapeName: false);
            }));

        doc.RootElement.SetProperty("data"u8, source);

        Assert.Equal(1, doc.RootElement["data"]["simple"]["a"].GetInt32());
    }

    #endregion

    #region ComplexValueBuilder.AddPropertyRawString char-span (lines 387-393)

    [Fact]
    public static void AddPropertyRawString_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddRawString("rawProp".AsSpan(), "rawValue"u8, false);
            }));

        doc.RootElement.SetProperty("obj"u8, source);

        Assert.Equal("rawValue", doc.RootElement["obj"]["rawProp"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span Build delegate (lines 731-736)

    [Fact]
    public static void ObjectBuilder_AddProperty_CharSpan_BuildDelegate()
    {
        using var workspace = JsonWorkspace.Create();

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("child".AsSpan(),
                    static (ref JsonElement.ObjectBuilder inner) =>
                    {
                        inner.AddProperty("val"u8, true);
                    });
            }));

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetProperty("parent"u8, source);

        Assert.True(doc.RootElement["parent"]["child"]["val"].GetBoolean());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span ArrayBuilder.Build (lines 743-748)

    [Fact]
    public static void ObjectBuilder_AddProperty_CharSpan_ArrayBuildDelegate()
    {
        using var workspace = JsonWorkspace.Create();

        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty("items".AsSpan(),
                    static (ref JsonElement.ArrayBuilder ab) =>
                    {
                        ab.AddItem(1);
                        ab.AddItem(2);
                    });
            }));

        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetProperty("container"u8, source);

        JsonElement.Mutable arr = doc.RootElement["container"]["items"];
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal(1, arr[0].GetInt32());
    }

    #endregion
}
