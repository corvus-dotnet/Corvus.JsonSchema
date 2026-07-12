// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="ParsedJsonDocumentBuilder"/> driven directly through
/// <see cref="ComplexValueBuilder"/>, validating that the single-pass materialization produces
/// documents identical to a serialize-and-reparse round trip of the equivalent
/// <see cref="JsonDocumentBuilder{T}"/>.
/// </summary>
[TestClass]
public class ParsedJsonDocumentBuilderTests
{
    #region Object creation

    [TestMethod]
    public void Build_SimpleObject_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("name"u8, "Alice"u8);
            cvb.AddProperty("age"u8, 30);
            cvb.AddProperty("active"u8, true);
            cvb.AddPropertyNull("notes"u8);
            cvb.EndObject();
        });

        Assert.AreEqual("""{"name":"Alice","age":30,"active":true,"notes":null}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Build_SimpleObject_SupportsPropertyLookupOnResult()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("name"u8, "Alice"u8);
            cvb.AddProperty("age"u8, 30);
            cvb.EndObject();
        });

        JsonElement root = doc.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.IsTrue(root.TryGetProperty("name"u8, out JsonElement name));
        Assert.IsTrue(name.ValueEquals("Alice"u8));
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
        Assert.IsFalse(root.TryGetProperty("missing"u8, out _));
    }

    [TestMethod]
    public void Build_EmptyObject_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartObject();
            cvb.EndObject();
        });

        Assert.AreEqual("{}", doc.RootElement.ToString());
        Assert.AreEqual(0, doc.RootElement.GetPropertyCount());
    }

    [TestMethod]
    public void Build_NestedObjectsAndArrays_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("address"u8, static (ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty("street"u8, "1 First Ave"u8);
                b.AddProperty("city"u8, "Metropolis"u8);
                b.EndObject();
            });
            cvb.AddProperty("tags"u8, static (ref ComplexValueBuilder b) =>
            {
                b.StartArray();
                b.AddItem("one"u8);
                b.AddItem(2);
                b.AddItem(static (ref ComplexValueBuilder inner) =>
                {
                    inner.StartObject();
                    inner.AddProperty("deep"u8, static (ref ComplexValueBuilder deepest) =>
                    {
                        deepest.StartArray();
                        deepest.AddItemNull();
                        deepest.AddItem(false);
                        deepest.EndArray();
                    });
                    inner.EndObject();
                });
                b.EndArray();
            });
            cvb.AddProperty("notes"u8, "hello"u8);
            cvb.EndObject();
        });

        Assert.AreEqual(
            """
            {"address":{"street":"1 First Ave","city":"Metropolis"},"tags":["one",2,{"deep":[null,false]}],"notes":"hello"}
            """,
            doc.RootElement.ToString());

        JsonElement tags = doc.RootElement.GetProperty("tags"u8);
        Assert.AreEqual(3, tags.GetArrayLength());
        Assert.AreEqual(2, tags[2].GetProperty("deep"u8).GetArrayLength());
    }

    #endregion

    #region Array creation

    [TestMethod]
    public void Build_SimpleArray_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartArray();
            cvb.AddItem("one"u8);
            cvb.AddItem(2);
            cvb.AddItem(true);
            cvb.AddItemNull();
            cvb.EndArray();
        });

        Assert.AreEqual("""["one",2,true,null]""", doc.RootElement.ToString());
        Assert.AreEqual(4, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void Build_EmptyArray_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartArray();
            cvb.EndArray();
        });

        Assert.AreEqual("[]", doc.RootElement.ToString());
        Assert.AreEqual(0, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void Build_ArrayOfArrays_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartArray();
            cvb.AddItem(static (ref ComplexValueBuilder b) =>
            {
                b.StartArray();
                b.AddItem(1);
                b.AddItem(2);
                b.EndArray();
            });
            cvb.AddItem(static (ref ComplexValueBuilder b) =>
            {
                b.StartArray();
                b.EndArray();
            });
            cvb.EndArray();
        });

        Assert.AreEqual("[[1,2],[]]", doc.RootElement.ToString());
    }

    #endregion

    #region Scalar roots

    [TestMethod]
    public void Build_ScalarStringRoot_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) => cvb.AddItem("hello"u8));

        Assert.AreEqual(JsonValueKind.String, doc.RootElement.ValueKind);
        Assert.IsTrue(doc.RootElement.ValueEquals("hello"u8));
        Assert.AreEqual("hello", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Build_ScalarNumberRoot_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) => cvb.AddItem(42));

        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.AreEqual(42, doc.RootElement.GetInt32());
    }

    [TestMethod]
    public void Build_ScalarBooleanRoot_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) => cvb.AddItem(false));

        Assert.AreEqual(JsonValueKind.False, doc.RootElement.ValueKind);
    }

    [TestMethod]
    public void Build_ScalarNullRoot_ProducesExpectedJson()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) => cvb.AddItemNull());

        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.ValueKind);
    }

    #endregion

    #region Scalar types

    [TestMethod]
    public void Build_FullScalarRange_RoundTripsThroughReparse()
    {
        var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff00");
        var dateTimeOffset = new DateTimeOffset(2026, 7, 12, 13, 30, 45, TimeSpan.FromHours(1));

        using ParsedJsonDocument<JsonElement> doc = BuildDocument((ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("sbyte"u8, (sbyte)-5);
            cvb.AddProperty("byte"u8, (byte)200);
            cvb.AddProperty("short"u8, (short)-3000);
            cvb.AddProperty("ushort"u8, (ushort)60000);
            cvb.AddProperty("int"u8, -123456);
            cvb.AddProperty("uint"u8, 4000000000u);
            cvb.AddProperty("long"u8, -9007199254740991L);
            cvb.AddProperty("ulong"u8, 18446744073709551615ul);
            cvb.AddProperty("float"u8, 1.5f);
            cvb.AddProperty("double"u8, -2.25d);
            cvb.AddProperty("decimal"u8, 123.456m);
            cvb.AddProperty("guid"u8, guid);
            cvb.AddProperty("dto"u8, in dateTimeOffset);
            cvb.AddPropertyFormattedNumber("formatted"u8, "1e3"u8);
            cvb.EndObject();
        });

        string json = doc.RootElement.ToString();
        using ParsedJsonDocument<JsonElement> reparsed = ParsedJsonDocument<JsonElement>.Parse(json);
        Assert.AreEqual(json, reparsed.RootElement.ToString());

        JsonElement root = doc.RootElement;
        Assert.AreEqual(-123456, root.GetProperty("int"u8).GetInt32());
        Assert.AreEqual(18446744073709551615ul, root.GetProperty("ulong"u8).GetUInt64());
        Assert.AreEqual(123.456m, root.GetProperty("decimal"u8).GetDecimal());
        Assert.IsTrue(root.GetProperty("guid"u8).ValueEquals("11223344-5566-7788-99aa-bbccddeeff00"u8));
        Assert.AreEqual(1000d, root.GetProperty("formatted"u8).GetDouble());
    }

    #endregion

    #region Escaping

    [TestMethod]
    public void Build_EscapedStringValuesAndNames_RoundTripCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("needs\nescaping"u8, "line1\nline2\t\"quoted\""u8);
            cvb.AddProperty("plain"u8, "no escaping needed"u8);
            cvb.EndObject();
        });

        JsonElement root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("needs\nescaping"u8, out JsonElement escaped));
        Assert.IsTrue(escaped.ValueEquals("line1\nline2\t\"quoted\""u8));

        // The document must re-parse to an identical document.
        string json = root.ToString();
        using ParsedJsonDocument<JsonElement> reparsed = ParsedJsonDocument<JsonElement>.Parse(json);
        Assert.IsTrue(reparsed.RootElement.GetProperty("needs\nescaping"u8).ValueEquals("line1\nline2\t\"quoted\""u8));
    }

    #endregion

    #region External document values

    [TestMethod]
    public void Build_EmbeddingParsedElements_BlitsContentIntoResult()
    {
        using var external = ParsedJsonDocument<JsonElement>.Parse("""{"a":[1,2,{"b":"c\nd"}],"s":"x"}""");
        JsonElement externalRoot = external.RootElement;
        JsonElement externalArray = externalRoot.GetProperty("a"u8);
        JsonElement externalString = externalRoot.GetProperty("s"u8);

        ParsedJsonDocument<JsonElement> doc;
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        try
        {
            ComplexValueBuilder cvb = ComplexValueBuilder.Create(builder, 30);
            cvb.StartObject();
            cvb.AddProperty("whole"u8, in externalRoot);
            cvb.AddProperty("array"u8, in externalArray);
            cvb.AddProperty("scalar"u8, in externalString);
            cvb.EndObject();
            ((IMutableJsonDocument)builder).SetAndDispose(ref cvb);
            doc = builder.ToParsedJsonDocument<JsonElement>();
        }
        finally
        {
            builder.Dispose();
        }

        using (doc)
        {
            Assert.AreEqual(
                """
                {"whole":{"a":[1,2,{"b":"c\nd"}],"s":"x"},"array":[1,2,{"b":"c\nd"}],"scalar":"x"}
                """,
                doc.RootElement.ToString());

            // The result must be fully self-contained: dispose the source, then read again.
            external.Dispose();
            Assert.IsTrue(doc.RootElement.GetProperty("whole"u8).GetProperty("a"u8)[2].GetProperty("b"u8).ValueEquals("c\nd"u8));
        }
    }

    [TestMethod]
    public void Build_EmbeddingMutableBuilderElements_BlitsContentIntoResult()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"keep":"me"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> mutable = workspace.CreateBuilder<JsonElement, JsonElement.Mutable>(sourceDoc.RootElement);
        mutable.RootElement.SetProperty("added"u8, 99);

        JsonElement.Mutable mutableRoot = mutable.RootElement;

        using ParsedJsonDocument<JsonElement> doc = BuildDocument((ref cvb) =>
        {
            cvb.StartArray();
            cvb.AddItem(in mutableRoot);
            cvb.EndArray();
        });

        Assert.AreEqual("""[{"keep":"me","added":99}]""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Build_TryApplyReplacingProperties_UsesPropertyLookupDuringConstruction()
    {
        // TryApply removes each incoming property before re-adding it, which drives the
        // builder's property-name lookup (TryGetNamedPropertyValueIndex over the in-progress
        // metadata) and the raw-string name store, mid-construction.
        using var overlay = ParsedJsonDocument<JsonElement>.Parse("""{"replace":"new","added":true}""");
        JsonElement overlayRoot = overlay.RootElement;

        using ParsedJsonDocument<JsonElement> doc = BuildDocument((ref cvb) =>
        {
            cvb.StartObject();
            cvb.AddProperty("keep"u8, 1);
            cvb.AddProperty("replace"u8, "old"u8);
            cvb.TryApply(overlayRoot);
            cvb.EndObject();
        });

        Assert.AreEqual("""{"keep":1,"replace":"new","added":true}""", doc.RootElement.ToString());

        string json = doc.RootElement.ToString();
        using ParsedJsonDocument<JsonElement> reparsed = ParsedJsonDocument<JsonElement>.Parse(json);
        Assert.AreEqual(json, reparsed.RootElement.ToString());
    }

    [TestMethod]
    public void Build_LargeExternalContent_GrowsTextBuffer()
    {
        // External content is not in the dynamic value store, so it is not accounted for by
        // the initial text-buffer estimate; a large embedded element forces the buffer to grow
        // mid-materialization.
        string bigValue = new string('x', 200_000);
        using var external = ParsedJsonDocument<JsonElement>.Parse($$"""{"big":"{{bigValue}}"}""");
        JsonElement externalRoot = external.RootElement;

        using ParsedJsonDocument<JsonElement> doc = BuildDocument((ref cvb) =>
        {
            cvb.StartArray();
            cvb.AddItem("small"u8);
            cvb.AddItem(in externalRoot);
            cvb.EndArray();
        });

        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
        Assert.IsTrue(doc.RootElement[1].GetProperty("big"u8).ValueEquals(bigValue));
    }

    [TestMethod]
    public void Build_AddItemFromJson_ParsesAndEmbedsContent()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref cvb) =>
        {
            cvb.StartArray();
            cvb.AddItemFromJson("""{"inner":[true,null,"x"]}"""u8);
            cvb.EndArray();
        });

        Assert.AreEqual("""[{"inner":[true,null,"x"]}]""", doc.RootElement.ToString());
    }

    #endregion

    #region Equivalence with JsonDocumentBuilder

    [TestMethod]
    public void Build_SameContent_MatchesJsonDocumentBuilderOutput()
    {
        static void Populate(ref ComplexValueBuilder cvb)
        {
            cvb.StartObject();
            cvb.AddProperty("s"u8, "value"u8);
            cvb.AddProperty("n"u8, 1.25d);
            cvb.AddProperty("b"u8, true);
            cvb.AddPropertyNull("z"u8);
            cvb.AddProperty("o"u8, static (ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty("nested"u8, "yesé"u8);
                b.EndObject();
            });
            cvb.AddProperty("a"u8, static (ref ComplexValueBuilder b) =>
            {
                b.StartArray();
                b.AddItem(1);
                b.AddItem("two"u8);
                b.EndArray();
            });
            cvb.EndObject();
        }

        // Build via the mutable document builder.
        using var workspace = JsonWorkspace.Create();
        JsonDocumentBuilder<JsonElement.Mutable> documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        ComplexValueBuilder mutableCvb = ComplexValueBuilder.Create(documentBuilder, 30);
        Populate(ref mutableCvb);
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref mutableCvb);
        string viaMutable = documentBuilder.RootElement.ToString();

        // Build the identical content via the parsed document builder.
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(Populate);

        Assert.AreEqual(viaMutable, doc.RootElement.ToString());
    }

    #endregion

    #region Lifetime and pooling

    [TestMethod]
    public void Dispose_WithoutToParsedJsonDocument_IsSafe()
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(builder, 4);
        cvb.StartObject();
        cvb.AddProperty("orphaned"u8, 1);
        cvb.EndObject();
        ((IMutableJsonDocument)builder).SetAndDispose(ref cvb);

        // Fault-handling path: no handoff occurred.
        builder.Dispose();

        // The pooled instance is reusable afterwards.
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(static (ref b) => b.AddItem(1));
        Assert.AreEqual("1", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Dispose_AfterToParsedJsonDocument_IsIdempotent()
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        ParsedJsonDocument<JsonElement> doc;
        try
        {
            ComplexValueBuilder cvb = ComplexValueBuilder.Create(builder, 4);
            cvb.AddItem("still valid"u8);
            ((IMutableJsonDocument)builder).SetAndDispose(ref cvb);
            doc = builder.ToParsedJsonDocument<JsonElement>();
        }
        finally
        {
            builder.Dispose();
            builder.Dispose();
        }

        using (doc)
        {
            Assert.IsTrue(doc.RootElement.ValueEquals("still valid"u8));
        }
    }

    [TestMethod]
    public void ToParsedJsonDocument_WithoutValue_Throws()
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => builder.ToParsedJsonDocument<JsonElement>());
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void ToParsedJsonDocument_AfterDispose_Throws()
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        builder.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => builder.ToParsedJsonDocument<JsonElement>());
    }

    [TestMethod]
    public void Rent_Nested_ProducesIndependentBuilders()
    {
        ParsedJsonDocumentBuilder outer = ParsedJsonDocumentBuilder.Rent();
        ParsedJsonDocument<JsonElement> outerDoc;
        try
        {
            ComplexValueBuilder outerCvb = ComplexValueBuilder.Create(outer, 8);
            outerCvb.StartArray();

            // A nested rental, as a build delegate calling another Create() would perform.
            using (ParsedJsonDocument<JsonElement> innerDoc = BuildDocument(static (ref b) => b.AddItem(123)))
            {
                Assert.AreEqual("123", innerDoc.RootElement.ToString());
            }

            outerCvb.AddItem("outer"u8);
            outerCvb.EndArray();
            ((IMutableJsonDocument)outer).SetAndDispose(ref outerCvb);
            outerDoc = outer.ToParsedJsonDocument<JsonElement>();
        }
        finally
        {
            outer.Dispose();
        }

        using (outerDoc)
        {
            Assert.AreEqual("""["outer"]""", outerDoc.RootElement.ToString());
        }
    }

    [TestMethod]
    public void Build_LargeDocument_GrowsBuffersCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = BuildDocument(
            static (ref cvb) =>
            {
                cvb.StartArray();
                for (int i = 0; i < 500; i++)
                {
                    int index = i;
                    cvb.AddItem(index, static (in int context, ref ComplexValueBuilder b) =>
                    {
                        b.StartObject();
                        b.AddProperty("index"u8, context);
                        b.AddProperty("text"u8, "some reasonably long text value to grow the buffer éé"u8);
                        b.EndObject();
                    });
                }

                cvb.EndArray();
            },
            initialCapacity: 4);

        Assert.AreEqual(500, doc.RootElement.GetArrayLength());
        Assert.AreEqual(499, doc.RootElement[499].GetProperty("index"u8).GetInt32());

        string json = doc.RootElement.ToString();
        using ParsedJsonDocument<JsonElement> reparsed = ParsedJsonDocument<JsonElement>.Parse(json);
        Assert.AreEqual(json, reparsed.RootElement.ToString());
    }

    #endregion

    private static ParsedJsonDocument<JsonElement> BuildDocument(ComplexValueBuilder.ValueBuilderAction populate, int initialCapacity = 30)
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilder.Rent();
        try
        {
            ComplexValueBuilder cvb = ComplexValueBuilder.Create(builder, initialCapacity);
            populate(ref cvb);
            ((IMutableJsonDocument)builder).SetAndDispose(ref cvb);
            return builder.ToParsedJsonDocument<JsonElement>();
        }
        finally
        {
            builder.Dispose();
        }
    }
}