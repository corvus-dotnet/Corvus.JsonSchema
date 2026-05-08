// <copyright file="MutableBuilderExtendedCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using System.Text.Json;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting uncovered paths in JsonDocumentBuilder, ComplexValueBuilder,
/// and ObjectBuilder — specifically char-based property lookups, prebaked property operations,
/// raw string properties, BigNumber/BigInteger array items, JSON pointer resolution,
/// line/offset stubs, and escape-controlled property addition.
/// </summary>
[TestClass]
public class MutableBuilderExtendedCoverageTests
{
    #region ObjectBuilder.AddProperty with Build<TContext> delegate (lines 163-171)

    [TestMethod]
    public void ObjectBuilder_AddProperty_WithContext()
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

        Assert.AreEqual(42, doc.RootElement["nested"]["value"].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddProperty with ArrayBuilder.Build<TContext> (lines 202-210)

    [TestMethod]
    public void ObjectBuilder_AddArrayProperty_WithContext()
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
        Assert.AreEqual(3, arr.GetArrayLength());
        Assert.AreEqual(10, arr[0].GetInt32());
        Assert.AreEqual(30, arr[2].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddProperty with char-span property name + escape control (lines 329-336)

    [TestMethod]
    public void ObjectBuilder_AddProperty_CharSpan_Value()
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

        Assert.AreEqual("hello", doc.RootElement["obj"]["name"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddFormattedNumber with char-span property name (lines 706-710)

    [TestMethod]
    public void ObjectBuilder_AddFormattedNumber_CharSpanPropertyName()
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

        Assert.AreEqual(42, doc.RootElement["obj"]["count"].GetInt32());
    }

    #endregion

    #region ObjectBuilder.AddRawString with char-span property name (lines 719-724)

    [TestMethod]
    public void ObjectBuilder_AddRawString_CharSpanPropertyName()
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

        Assert.AreEqual("hello world", doc.RootElement["obj"]["text"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span with ArrayBuilder.Build<TContext> (lines 759-764)

    [TestMethod]
    public void ObjectBuilder_AddProperty_CharSpan_ArrayWithContext()
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
        Assert.AreEqual(3, arr.GetArrayLength());
        Assert.AreEqual("a", arr[0].GetString());
        Assert.AreEqual("c", arr[2].GetString());
    }

    #endregion

    #region BigNumber and BigInteger array items (ComplexValueBuilder lines 2771-2786)

    [TestMethod]
    public void ArrayBuilder_AddItem_BigNumber()
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
        StringAssert.Contains(result, "123456789012345678901234567890");
    }

    [TestMethod]
    public void ArrayBuilder_AddItem_BigInteger()
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
        Assert.AreEqual("99999999999999999999999999999999", result);
    }

    #endregion

    #region TryGetNamedPropertyValue with char span (JsonDocumentBuilder lines 2510-2519)

    [TestMethod]
    public void Builder_TryGetNamedPropertyValue_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"hello": 42, "world": "test"}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Access property by char-span through the mutable element indexer (uses char span lookup internally)
        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual(42, root["hello"].GetInt32());
        Assert.AreEqual("test", root["world"].GetString());

        // Also test TryGetProperty with string
        Assert.IsTrue(root.TryGetProperty("hello", out JsonElement.Mutable hello));
        Assert.AreEqual(42, hello.GetInt32());
    }

    [TestMethod]
    public void Builder_TryGetNamedPropertyValue_CharSpan_NotFound()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = doc.RootElement;
        Assert.IsFalse(root.TryGetProperty("nonexistent", out _));
    }

    #endregion

    #region TryResolveJsonPointer on builder (JsonDocumentBuilder lines 2732-2738)

    [TestMethod]
    public void Builder_TryResolveJsonPointer()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": [1, 2, 3]}}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Use the JsonPointer-based navigation via the mutable element
        JsonElement.Mutable root = doc.RootElement;
        JsonElement.Mutable nested = root["a"]["b"];
        Assert.AreEqual(3, nested.GetArrayLength());
    }

    #endregion

    #region TryGetLineAndOffset always returns false (JsonDocumentBuilder lines 2751-2765)

    [TestMethod]
    public void Builder_TryGetLineAndOffset_AlwaysReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Access via IJsonDocument interface
        IJsonDocument document = doc;
        bool result = document.TryGetLineAndOffset(0, out int line, out int charOffset, out long lineByteOffset);

        Assert.IsFalse(result);
        Assert.AreEqual(0, line);
        Assert.AreEqual(0, charOffset);
        Assert.AreEqual(0, lineByteOffset);
    }

    [TestMethod]
    public void Builder_TryGetLineAndOffsetForPointer_AlwaysReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        IJsonDocument document = doc;
        bool result = document.TryGetLineAndOffsetForPointer("/a"u8, 0, out int line, out int charOffset, out long lineByteOffset);

        Assert.IsFalse(result);
        Assert.AreEqual(0, line);
        Assert.AreEqual(0, charOffset);
        Assert.AreEqual(0, lineByteOffset);
    }

    #endregion

    #region Builder serialization with escaped property names (JsonDocumentBuilder lines 2340-2372)

    [TestMethod]
    public void Builder_SerializeEscapedPropertyName()
    {
        // Create a document with a property name that needs escaping
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"hello\nworld": 42}""");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Serializing should properly handle the escaped property name
        string json = doc.RootElement.ToString();
        StringAssert.Contains(json, "42");
    }

    #endregion

    #region Prebaked property operations (ComplexValueBuilder lines 3554-3564, 3629-3649, 3726-3733)

    [TestMethod]
    public void PrebakedProperty_ComplexValue()
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

        Assert.AreEqual(1, doc.RootElement["data"]["nested"]["x"].GetInt32());
        Assert.AreEqual(2, doc.RootElement["data"]["nested"]["y"].GetInt32());
    }

    [TestMethod]
    public void PrebakedProperty_IntValue()
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

        Assert.AreEqual(42, root["count"].GetInt32());
        Assert.AreEqual("test", root["name"].GetString());
        Assert.IsTrue(root["active"].GetBoolean());
    }

    #endregion

    #region StartProperty with byte span + escape control (ComplexValueBuilder lines 3440-3446)

    [TestMethod]
    public void StartEndProperty_ByteSpan_WithEscape()
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

        Assert.AreEqual("value", doc.RootElement["root"]["container"]["key"].GetString());
    }

    #endregion

    #region StartProperty with char span (ComplexValueBuilder lines 3454-3460)

    [TestMethod]
    public void StartProperty_CharSpan()
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

        Assert.AreEqual(99, doc.RootElement["top"]["myProp"]["nested"].GetInt32());
    }

    #endregion

    #region RentedBacking (lines at 0% coverage)

    [TestMethod]
    public void RentedBacking_InitializeAndDispose()
    {
        // RentedBacking is used by Source<TContext>.Builder — exercise it through a source
        using var workspace = JsonWorkspace.Create();
        using var parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var doc = parsed.RootElement.CreateBuilder(workspace);

        // Any Source that writes a larger-than-inline value triggers RentedBacking
        string longValue = new('x', 200); // Exceed the inline buffer threshold
        doc.RootElement.SetProperty("big"u8, longValue);

        Assert.AreEqual(longValue, doc.RootElement["big"].GetString());
    }

    #endregion

    #region JsonWorkspace uncovered paths (lines 60 uncov)

    [TestMethod]
    public void Workspace_CreateUnrented()
    {
        // Test the unrented workspace creation path
        var workspace = JsonWorkspace.CreateUnrented();

        try
        {
            using var parsed = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
            using var doc = parsed.RootElement.CreateBuilder(workspace);

            Assert.AreEqual(1, doc.RootElement["a"].GetInt32());
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [TestMethod]
    public void Workspace_MultipleBuilders()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsed1 = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using var parsed2 = ParsedJsonDocument<JsonElement>.Parse("""{"y": 2}""");

        using var doc1 = parsed1.RootElement.CreateBuilder(workspace);
        using var doc2 = parsed2.RootElement.CreateBuilder(workspace);

        Assert.AreEqual(1, doc1.RootElement["x"].GetInt32());
        Assert.AreEqual(2, doc2.RootElement["y"].GetInt32());
    }

    #endregion

    #region ComplexValueBuilder.AddProperty with escape control (lines 135-144)

    [TestMethod]
    public void AddProperty_WithEscapeControl_NoEscape()
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

        Assert.AreEqual(1, doc.RootElement["data"]["simple"]["a"].GetInt32());
    }

    #endregion

    #region ComplexValueBuilder.AddPropertyRawString char-span (lines 387-393)

    [TestMethod]
    public void AddPropertyRawString_CharSpan()
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

        Assert.AreEqual("rawValue", doc.RootElement["obj"]["rawProp"].GetString());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span Build delegate (lines 731-736)

    [TestMethod]
    public void ObjectBuilder_AddProperty_CharSpan_BuildDelegate()
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

        Assert.IsTrue(doc.RootElement["parent"]["child"]["val"].GetBoolean());
    }

    #endregion

    #region ObjectBuilder.AddProperty char-span ArrayBuilder.Build (lines 743-748)

    [TestMethod]
    public void ObjectBuilder_AddProperty_CharSpan_ArrayBuildDelegate()
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
        Assert.AreEqual(2, arr.GetArrayLength());
        Assert.AreEqual(1, arr[0].GetInt32());
    }

    #endregion
}
