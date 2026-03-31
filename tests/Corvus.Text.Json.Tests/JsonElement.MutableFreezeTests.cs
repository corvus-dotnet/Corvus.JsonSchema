// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Reflection;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public static class JsonElementMutableFreezeTests
{
    [Fact]
    public static void FreezeAtInnerArray()
    {
        FreezeAtInner(
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
    public static void FreezeAtInnerFalse()
    {
        FreezeAtInner("false", JsonValueKind.False);
    }

    [Fact]
    public static void FreezeAtInnerNull()
    {
        FreezeAtInner("null", JsonValueKind.Null);
    }

    [Fact]
    public static void FreezeAtInnerNumber()
    {
        FreezeAtInner("1.21e9", JsonValueKind.Number);
    }

    [Fact]
    public static void FreezeAtInnerObject()
    {
        FreezeAtInner(
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
    public static void FreezeAtInnerString()
    {
        FreezeAtInner("\"  this  string  has  \\u0039 spaces\"", JsonValueKind.String);
    }

    [Fact]
    public static void FreezeAtInnerTrue()
    {
        FreezeAtInner("true", JsonValueKind.True);
    }

    [Fact]
    public static void FreezeRootElement()
    {
        string json = """{"name":"test","value":42}""";

        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement frozen = doc.RootElement.Freeze();

        Assert.Equal(JsonValueKind.Object, frozen.ValueKind);
        Assert.Equal(json, frozen.GetRawText());
    }

    [Fact]
    public static void FreezeInnerElementFromFrozenElement()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[[]]]");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement middle = doc.RootElement[0].Freeze();
        JsonElement inner = middle[0];

        Assert.Equal("[]", inner.GetRawText());
    }

    [Fact]
    public static void FreezeTwiceFromSameDocument()
    {
        string json = "[[]]";

        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = doc.RootElement;
        JsonElement frozen1 = root.Freeze();
        JsonElement frozen2 = root.Freeze();

        Assert.Equal(json, frozen1.GetRawText());
        Assert.Equal(json, frozen2.GetRawText());

        // Frozen copies are backed by different documents.
        Assert.NotSame(
            SniffParentDocument(frozen1),
            SniffParentDocument(frozen2));
    }

    [Fact]
    public static void FreezeAfterMutation()
    {
        string json = """{"name":"original"}""";

        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        // Freeze before mutation.
        JsonElement frozenBefore = doc.RootElement.Freeze();

        // Mutate the document.
        doc.RootElement.SetProperty("name", "modified");

        // Freeze after mutation.
        JsonElement frozenAfter = doc.RootElement.Freeze();

        // The frozen-before snapshot should retain the original value.
        Assert.Equal("original", frozenBefore.GetProperty("name").GetString());

        // The frozen-after snapshot should have the modified value.
        Assert.Equal("modified", frozenAfter.GetProperty("name").GetString());
    }

    [Fact]
    public static void FrozenElementIsImmutable()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"test"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        // Mutate so the root element has local dynamic values.
        doc.RootElement.SetProperty("added", "value");

        JsonElement frozen = doc.RootElement.Freeze();

        // The frozen element should have the added property.
        Assert.Equal("value", frozen.GetProperty("added").GetString());
        Assert.Equal("test", frozen.GetProperty("name").GetString());
    }

    [Fact]
    public static void FrozenElementSurvivesSourceBuilderDispose()
    {
        string json = """{"key":"value"}""";
        JsonElement frozen;

        using (var workspace = JsonWorkspace.Create())
        {
            using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
            using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

            frozen = doc.RootElement.Freeze();

            // Dispose the source builder explicitly.
            doc.Dispose();

            // Frozen element should still be accessible within the workspace.
            Assert.Equal(json, frozen.GetRawText());
        }
    }

    [Fact]
    public static void FreezeSimpleValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("""[42, "hello", true, false, null]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal(42, root[0].Freeze().GetInt32());
        Assert.Equal("hello", root[1].Freeze().GetString());
        Assert.True(root[2].Freeze().GetBoolean());
        Assert.False(root[3].Freeze().GetBoolean());
        Assert.Equal(JsonValueKind.Null, root[4].Freeze().ValueKind);
    }

    [Fact]
    public static void FreezeNestedObject()
    {
        string json = """{"outer":{"inner":{"deep":"value"}}}""";

        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement frozenInner = doc.RootElement.GetProperty("outer").GetProperty("inner").Freeze();

        Assert.Equal("""{"deep":"value"}""", frozenInner.GetRawText());
        Assert.Equal("value", frozenInner.GetProperty("deep").GetString());
    }

    [Fact]
    public static void FreezeWithDynamicValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{}");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        // Add dynamic values.
        doc.RootElement.SetProperty("added", "dynamic-value");
        doc.RootElement.SetProperty("number", 123);

        JsonElement frozen = doc.RootElement.Freeze();

        Assert.Equal("dynamic-value", frozen.GetProperty("added").GetString());
        Assert.Equal(123, frozen.GetProperty("number").GetInt32());
    }

    private static void FreezeAtInner(string innerJson, JsonValueKind valueType)
    {
        string json = $"{{ \"obj\": [ {{ \"not target\": true, \"target\": {innerJson} }}, 5 ] }}";

        using var workspace = JsonWorkspace.Create();
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable target = doc.RootElement.GetProperty("obj")[0].GetProperty("target");
        Assert.Equal(valueType, target.ValueKind);

        JsonElement frozen = target.Freeze();
        Assert.Equal(valueType, frozen.ValueKind);

        // Frozen element should match the clone output for the same element.
        JsonElement cloned = target.Clone();
        Assert.Equal(cloned.GetRawText(), frozen.GetRawText());
    }

    private static IJsonDocument SniffParentDocument(JsonElement element)
    {
        return (IJsonDocument)typeof(JsonElement)
            .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(element)!;
    }
}