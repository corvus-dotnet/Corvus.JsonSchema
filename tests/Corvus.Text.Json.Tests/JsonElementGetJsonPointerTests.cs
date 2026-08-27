// <copyright file="JsonElementGetJsonPointerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for deriving the JSON Pointer (RFC 6901) of an element relative to its document root
/// via <see cref="JsonElement.GetJsonPointer"/> and the TryGetJsonPointer span overloads.
/// </summary>
[TestClass]
public class JsonElementGetJsonPointerTests
{
    private const string Rfc6901ExampleDocument = """
        {
            "foo": ["bar", "baz"],
            "": 0,
            "a/b": 1,
            "c%d": 2,
            "e^f": 3,
            "g|h": 4,
            "i\\j": 5,
            "k\"l": 6,
            " ": 7,
            "m~n": 8
        }
        """;

    private const string StoreDocument = """
        {"store":{"book":[{"title":"A","price":10},{"title":"B","price":5}]}}
        """;

    [TestMethod]
    public void RootElement_ProducesEmptyPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        Assert.AreEqual(string.Empty, doc.RootElement.GetJsonPointer());
    }

    [TestMethod]
    public void RootScalarDocument_ProducesEmptyPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        Assert.AreEqual(string.Empty, doc.RootElement.GetJsonPointer());
    }

    [TestMethod]
    [DataRow("/foo")]
    [DataRow("/foo/0")]
    [DataRow("/foo/1")]
    [DataRow("/")]
    [DataRow("/a~1b")]
    [DataRow("/c%d")]
    [DataRow("/e^f")]
    [DataRow("/g|h")]
    [DataRow("/i\\j")]
    [DataRow("/k\"l")]
    [DataRow("/ ")]
    [DataRow("/m~0n")]
    public void Rfc6901ExamplePointers_RoundTripThroughResolveAndGet(string pointer)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Rfc6901ExampleDocument);
        Assert.IsTrue(doc.RootElement.TryResolvePointer(pointer, out JsonElement resolved));
        Assert.AreEqual(pointer, resolved.GetJsonPointer());
    }

    [TestMethod]
    public void NestedElement_ProducesFullPathFromRoot()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);
        JsonElement title = doc.RootElement["store"u8]["book"u8][1]["title"u8];
        Assert.AreEqual("/store/book/1/title", title.GetJsonPointer());
    }

    [TestMethod]
    public void ContainerElement_ProducesItsOwnPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);
        JsonElement book = doc.RootElement["store"u8]["book"u8];
        Assert.AreEqual("/store/book", book.GetJsonPointer());
    }

    [TestMethod]
    public void ElementAfterContainerSiblings_CountsThemAsSingleValues()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """[{"a":1},[2,3],"x",{"b":{"c":[4]}},5]""");
        Assert.AreEqual("/4", doc.RootElement[4].GetJsonPointer());
        Assert.AreEqual("/3/b/c/0", doc.RootElement[3]["b"u8]["c"u8][0].GetJsonPointer());
        Assert.AreEqual("/1/1", doc.RootElement[1][1].GetJsonPointer());
    }

    [TestMethod]
    public void ObjectValueAfterContainerProperties_WalksOverThem()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":{"b":1},"c":[2,3],"d":4}""");
        Assert.AreEqual("/d", doc.RootElement["d"u8].GetJsonPointer());
        Assert.AreEqual("/c/1", doc.RootElement["c"u8][1].GetJsonPointer());
        Assert.AreEqual("/a/b", doc.RootElement["a"u8]["b"u8].GetJsonPointer());
    }

    [TestMethod]
    public void MultiDigitArrayIndex_FormatsAllDigits()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14]");
        Assert.AreEqual("/12", doc.RootElement[12].GetJsonPointer());
    }

    [TestMethod]
    public void EscapedPropertyName_IsUnescapedBeforePointerEscaping()
    {
        // The parsed property names are "a\nb" (a real newline) and "café" (via é).
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a\nb":{"café":1}}""");
        Assert.AreEqual("/a\nb/café", doc.RootElement["a\nb"]["café"].GetJsonPointer());
    }

    [TestMethod]
    public void PropertyNameContainingSlashAndTilde_AppliesPointerEscaping()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"~/":1}""");
        Assert.AreEqual("/~0~1", doc.RootElement["~/"].GetJsonPointer());
    }

    [TestMethod]
    public void JsonEscapedPropertyNameContainingSlashAndTilde_UnescapesThenPointerEscapes()
    {
        // The parsed property name is "~/" written with JSON \u escapes, so the
        // metadata row carries the requires-unescaping flag.
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            "{\"\\u007e\\u002f\":1}");
        Assert.AreEqual("/~0~1", doc.RootElement["~/"].GetJsonPointer());
    }

    [TestMethod]
    public void NonAsciiPropertyName_RoundTripsInBothEncodings()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"日本語":[null,{"emoji 🎉":true}]}""");
        JsonElement element = doc.RootElement["日本語"][1]["emoji 🎉"];

        Assert.AreEqual("/日本語/1/emoji 🎉", element.GetJsonPointer());

        Span<byte> utf8Buffer = stackalloc byte[128];
        Assert.IsTrue(element.TryGetJsonPointer(utf8Buffer, out int bytesWritten));
        Assert.AreEqual("/日本語/1/emoji 🎉", Encoding.UTF8.GetString(utf8Buffer.Slice(0, bytesWritten).ToArray()));

        Span<char> charBuffer = stackalloc char[128];
        Assert.IsTrue(element.TryGetJsonPointer(charBuffer, out int charsWritten));
        Assert.AreEqual("/日本語/1/emoji 🎉", charBuffer.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void Utf8Destination_ExactSizeSucceeds_OneByteShortFails()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);
        JsonElement title = doc.RootElement["store"u8]["book"u8][0]["title"u8];

        int exactLength = Encoding.UTF8.GetByteCount("/store/book/0/title");
        Span<byte> exact = stackalloc byte[exactLength];
        Assert.IsTrue(title.TryGetJsonPointer(exact, out int bytesWritten));
        Assert.AreEqual(exactLength, bytesWritten);

        Span<byte> short1 = stackalloc byte[exactLength - 1];
        Assert.IsFalse(title.TryGetJsonPointer(short1, out bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void CharDestination_ExactSizeSucceeds_OneCharShortFails()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);
        JsonElement title = doc.RootElement["store"u8]["book"u8][0]["title"u8];

        Span<char> exact = stackalloc char["/store/book/0/title".Length];
        Assert.IsTrue(title.TryGetJsonPointer(exact, out int charsWritten));
        Assert.AreEqual("/store/book/0/title".Length, charsWritten);

        Span<char> short1 = stackalloc char["/store/book/0/title".Length - 1];
        Assert.IsFalse(title.TryGetJsonPointer(short1, out charsWritten));
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void EmptyDestination_SucceedsForRoot()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(doc.RootElement.TryGetJsonPointer(Span<byte>.Empty, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
        Assert.IsTrue(doc.RootElement.TryGetJsonPointer(Span<char>.Empty, out int charsWritten));
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void DefaultElement_TryReturnsFalse_GetThrows()
    {
        JsonElement element = default;
        Assert.IsFalse(element.TryGetJsonPointer(new byte[16], out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
        Assert.IsFalse(element.TryGetJsonPointer(new char[16], out int charsWritten));
        Assert.AreEqual(0, charsWritten);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetJsonPointer());
    }

    [TestMethod]
    public void DisposedDocument_Throws()
    {
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement element = doc.RootElement["a"u8];
        doc.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetJsonPointer());
    }

    [TestMethod]
    public void PointerLongerThanStackBuffer_UsesExactSizedRetry()
    {
        string longName = new string('x', 300);
        string json = string.Concat("{\"", longName, "\":{\"inner\":1}}");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        Assert.AreEqual($"/{longName}/inner", doc.RootElement[longName]["inner"].GetJsonPointer());
    }

    [TestMethod]
    public void DepthBeyondSegmentStackCapacity_GrowsAndSucceeds()
    {
        const int Depth = 100;
        var options = new JsonDocumentOptions { MaxDepth = Depth + 8 };
        string json = string.Concat(new string('[', Depth), "7", new string(']', Depth));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json, options);

        JsonElement element = doc.RootElement;
        for (int i = 0; i < Depth; i++)
        {
            element = element[0];
        }

        string expected = string.Concat(System.Linq.Enumerable.Repeat("/0", Depth));
        Assert.AreEqual(expected, element.GetJsonPointer());
        Assert.IsTrue(doc.RootElement.TryResolvePointer(expected, out JsonElement resolved));
        Assert.AreEqual(7, resolved.GetInt32());
    }

    [TestMethod]
    public void PropertyNameElement_ProducesThePropertyPointer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":{"b":1},"c":2}""");
        JsonElement value = doc.RootElement["c"u8];
        IJsonElement valueAccessor = value;
        JsonElement nameElement = valueAccessor.ParentDocument.GetPropertyName(valueAccessor.ParentDocumentIndex);
        Assert.AreEqual("/c", nameElement.GetJsonPointer());
    }

    [TestMethod]
    public void ParseValueElement_IsItsOwnRoot()
    {
        using ParsedJsonDocument<JsonElement> outer = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":{"b":1}}""");
        using ParsedJsonDocument<JsonElement> clone = ParsedJsonDocument<JsonElement>.Parse("""{"b":1}""");
        Assert.AreEqual(string.Empty, clone.RootElement.GetJsonPointer());
        Assert.AreEqual("/b", clone.RootElement["b"u8].GetJsonPointer());
    }

    [TestMethod]
    public void ExtensionOverloads_WorkForJsonElement()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);
        JsonElement price = doc.RootElement["store"u8]["book"u8][1]["price"u8];

        Assert.AreEqual("/store/book/1/price", JsonElementExtensions.GetJsonPointer(price));

        Span<byte> utf8Buffer = stackalloc byte[64];
        Assert.IsTrue(JsonElementExtensions.TryGetJsonPointer(price, utf8Buffer, out int bytesWritten));
        Assert.AreEqual("/store/book/1/price", Encoding.UTF8.GetString(utf8Buffer.Slice(0, bytesWritten).ToArray()));

        Span<char> charBuffer = stackalloc char[64];
        Assert.IsTrue(JsonElementExtensions.TryGetJsonPointer(price, charBuffer, out int charsWritten));
        Assert.AreEqual("/store/book/1/price", charBuffer.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void BuilderDocument_LocalAndDynamicValues_ProducePointers()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":{"b":[1,2]}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("z", 42);

        JsonElement.Mutable z = root["z"];
        Assert.AreEqual("/z", z.GetJsonPointer());

        Span<byte> utf8Buffer = stackalloc byte[16];
        Assert.IsTrue(z.TryGetJsonPointer(utf8Buffer, out int bytesWritten));
        Assert.AreEqual("/z", Encoding.UTF8.GetString(utf8Buffer.Slice(0, bytesWritten).ToArray()));
    }

    [TestMethod]
    public void BuilderDocument_ValueAfterExternalContainer_WalksOverMirroredRows()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> external = ParsedJsonDocument<JsonElement>.Parse(
            """{"p":[10,20]}""");
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{}");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("ext", external.RootElement);
        root.SetProperty("z", 1);

        // The walk from "z" crosses the mirrored external container rows via its End row.
        Assert.AreEqual("/z", root["z"].GetJsonPointer());
    }

    [TestMethod]
    public void FrozenBuilderDocument_ProducesPointers()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> external = ParsedJsonDocument<JsonElement>.Parse(
            """{"p":[10,20]}""");
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{}");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("ext", external.RootElement);
        root.SetProperty("z", 1);

        JsonElement frozenRoot = root.Freeze();
        Assert.AreEqual("/ext/p/1", frozenRoot["ext"u8]["p"u8][1].GetJsonPointer());
    }

    [TestMethod]
    public void PointerRoundTrip_ResolvesToTheSameValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":[{"m~n":{"x/y":[true,false,{"":null}]}}]}""");
        JsonElement element = doc.RootElement["a"u8][0]["m~n"]["x/y"][2][""];
        string pointer = element.GetJsonPointer();
        Assert.AreEqual("/a/0/m~0n/x~1y/2/", pointer);
        Assert.IsTrue(doc.RootElement.TryResolvePointer(pointer, out JsonElement resolved));
        Assert.AreEqual(JsonValueKind.Null, resolved.ValueKind);
    }
}