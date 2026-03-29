// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Reflection;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public static class JsonElementMutableDynamicCloneTests
{
    [Fact]
    public static void CloneAtInnerArray()
    {
        // Very weird whitespace is used here just to ensure that the
        // clone API isn't making any whitespace assumptions.
        CloneAtInner(
            @"[
{
  ""this"":
  [
    {
      ""object"": 0,

      ""has"": [ ""whitespace"" ]
    }
  ]
},

5

,

false,

null
]",
            JsonValueKind.Array);
    }

    [Fact]
    public static void CloneAtInnerFalse()
    {
        CloneAtInner("false", JsonValueKind.False);
    }

    [Fact]
    public static void CloneAtInnerNull()
    {
        CloneAtInner("null", JsonValueKind.Null);
    }

    [Fact]
    public static void CloneAtInnerNumber()
    {
        CloneAtInner("1.21e9", JsonValueKind.Number);
    }

    [Fact]
    public static void CloneAtInnerObject()
    {
        // Very weird whitespace is used here just to ensure that the
        // clone API isn't making any whitespace assumptions.
        CloneAtInner(
            @"{
  ""this"":
  [
    {
      ""object"": 0,

      ""has"": [ ""whitespace"" ]
    }
  ]
}",
            JsonValueKind.Object);
    }

    [Fact]
    public static void CloneAtInnerString()
    {
        CloneAtInner("\"  this  string  has  \\u0039 spaces\"", JsonValueKind.String);
    }

    [Fact]
    public static void CloneAtInnerTrue()
    {
        CloneAtInner("true", JsonValueKind.True);
    }

    [Fact]
    public static void CloneInnerElementFromClonedElement()
    {
        JsonElement clone;

        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[[]]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement middle = doc.RootElement[0].Clone();
            JsonElement inner = middle[0];
            clone = inner.Clone();

            Assert.Equal(inner.GetRawText(), clone.GetRawText());
            Assert.NotSame(doc, clone.SniffDocument());
            Assert.Same(middle.SniffDocument(), clone.SniffDocument());
            Assert.Same(inner.SniffDocument(), clone.SniffDocument());
            Assert.False(clone.SniffDocument().IsDisposable());
        }

        // After document Dispose
        Assert.Equal("[]", clone.GetRawText());
    }

    [Fact]
    public static void CloneTwiceFromSameDocument()
    {
        string json = "[[]]";
        JsonElement.Mutable root;
        JsonElement clone;
        JsonElement clone2;

        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            root = doc.RootElement;
            clone = root.Clone();
            clone2 = root.Clone();

            Assert.Equal(json, clone.GetRawText());
            Assert.NotSame(doc, clone.SniffDocument());
            Assert.NotSame(doc, clone2.SniffDocument());

            Assert.False(clone.SniffDocument().IsDisposable());
            Assert.False(clone2.SniffDocument().IsDisposable());
        }

        // After document Dispose
        Assert.Equal(json, clone.GetRawText());
        Assert.Equal(json, clone2.GetRawText());
        Assert.NotSame(clone.SniffDocument(), clone2.SniffDocument());

        Assert.Throws<ObjectDisposedException>(() => root.GetRawText());
    }

    internal static bool IsDisposable(this JsonDocumentBuilder<JsonElement.Mutable> document)
    {
        return ((IJsonDocument)document).IsDisposable;
    }

    internal static JsonDocumentBuilder<JsonElement.Mutable> SniffDocument(this JsonElement.Mutable element)
    {
        return (JsonDocumentBuilder<JsonElement.Mutable>)typeof(JsonElement.Mutable)
            .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(element);
    }

    private static void CloneAtInner(string innerJson, JsonValueKind valueType)
    {
        string json = $"{{ \"obj\": [ {{ \"not target\": true, \"target\": {innerJson} }}, 5 ] }}";

        JsonElement clone;
        string rawTarget;

        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable target = doc.RootElement.GetProperty("obj")[0].GetProperty("target");
            Assert.Equal(valueType, target.ValueKind);
            clone = target.Clone();
            rawTarget = target.GetRawText();
        }

        Assert.Equal(rawTarget, clone.GetRawText());
    }
}