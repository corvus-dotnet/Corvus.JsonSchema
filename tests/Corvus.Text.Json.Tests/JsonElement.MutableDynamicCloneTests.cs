// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Reflection;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementMutableDynamicCloneTests
{
    [TestMethod]
    public void CloneAtInnerArray()
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

    [TestMethod]
    public void CloneAtInnerFalse()
    {
        CloneAtInner("false", JsonValueKind.False);
    }

    [TestMethod]
    public void CloneAtInnerNull()
    {
        CloneAtInner("null", JsonValueKind.Null);
    }

    [TestMethod]
    public void CloneAtInnerNumber()
    {
        CloneAtInner("1.21e9", JsonValueKind.Number);
    }

    [TestMethod]
    public void CloneAtInnerObject()
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

    [TestMethod]
    public void CloneAtInnerString()
    {
        CloneAtInner("\"  this  string  has  \\u0039 spaces\"", JsonValueKind.String);
    }

    [TestMethod]
    public void CloneAtInnerTrue()
    {
        CloneAtInner("true", JsonValueKind.True);
    }

    [TestMethod]
    public void CloneInnerElementFromClonedElement()
    {
        JsonElement clone;

        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[[]]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement middle = doc.RootElement[0].Clone();
            JsonElement inner = middle[0];
            clone = inner.Clone();

            Assert.AreEqual(inner.GetRawText(), clone.GetRawText());
            Assert.AreNotSame((object)doc, (object?)clone.SniffDocument());
            Assert.AreSame((object?)middle.SniffDocument(), (object?)clone.SniffDocument());
            Assert.AreSame((object?)inner.SniffDocument(), (object?)clone.SniffDocument());
            Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)clone.SniffDocument()).IsDisposable);
        }

        // After document Dispose
        Assert.AreEqual("[]", clone.GetRawText());
    }

    [TestMethod]
    public void CloneTwiceFromSameDocument()
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

            Assert.AreEqual(json, clone.GetRawText());
            Assert.AreNotSame((object)doc, (object?)clone.SniffDocument());
            Assert.AreNotSame((object)doc, (object?)clone2.SniffDocument());

            Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)clone.SniffDocument()).IsDisposable);
            Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)clone2.SniffDocument()).IsDisposable);
        }

        // After document Dispose
        Assert.AreEqual(json, clone.GetRawText());
        Assert.AreEqual(json, clone2.GetRawText());
        Assert.AreNotSame(clone.SniffDocument(), clone2.SniffDocument());

        Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetRawText());
    }

    internal static bool IsDisposable(JsonDocumentBuilder<JsonElement.Mutable> document)
    {
        return ((IJsonDocument)document).IsDisposable;
    }

    internal static JsonDocumentBuilder<JsonElement.Mutable> SniffDocument(JsonElement.Mutable element)
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
            Assert.AreEqual(valueType, target.ValueKind);
            clone = target.Clone();
            rawTarget = target.GetRawText();
        }

        Assert.AreEqual(rawTarget, clone.GetRawText());
    }
}
