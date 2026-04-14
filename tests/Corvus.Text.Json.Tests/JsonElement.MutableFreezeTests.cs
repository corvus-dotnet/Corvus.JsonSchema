// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public static class JsonElementMutableFreezeTests
{
    [Fact]
    public static void FreezeAtInnerArray()
    {
        FreezeAtInner(
            """
            [
            {
              "this":
              [
                {
                  "object": 0,

                  "has": [ "whitespace" ]
                }
              ]
            },

            5

            ,

            false,

            null
            ]
            """,
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
            """
            {
              "this":
              [
                {
                  "object": 0,

                  "has": [ "whitespace" ]
                }
              ]
            }
            """,
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

    [Fact]
    public static void FreezeImmutableElementReturnsThis()
    {
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement original = parsedDoc.RootElement;

        JsonElement frozen = original.Freeze();

        // Already immutable, so Freeze() returns the same instance.
        Assert.Equal(original.GetRawText(), frozen.GetRawText());
        Assert.Same(SniffParentDocument(original), SniffParentDocument(frozen));
    }

    [Fact]
    public static void FreezeImmutableClonedElementReturnsThis()
    {
        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x":"y"}""");

        // Clone produces an immutable element backed by a non-disposable document.
        JsonElement cloned = parsedDoc.RootElement.Clone();
        JsonElement frozen = cloned.Freeze();

        Assert.Equal(cloned.GetRawText(), frozen.GetRawText());
        Assert.Same(SniffParentDocument(cloned), SniffParentDocument(frozen));
    }

    [Fact]
    public static void FreezeCrossDocumentObjectProperty()
    {
        // Assign a property value from one mutable document into another,
        // then freeze the target.
        using var workspace = JsonWorkspace.Create();

        using var srcParsed = ParsedJsonDocument<JsonElement>.Parse(
            """{"payload":{"inner":"sourceValue","count":99}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> srcDoc =
            srcParsed.RootElement.CreateBuilder(workspace);

        using var tgtParsed = ParsedJsonDocument<JsonElement>.Parse("""{"target":null}""");
        using JsonDocumentBuilder<JsonElement.Mutable> tgtDoc =
            tgtParsed.RootElement.CreateBuilder(workspace);

        // Copy the "payload" object from src into tgt as "target".
        JsonElement.Mutable payload = srcDoc.RootElement.GetProperty("payload");
        tgtDoc.RootElement.SetProperty("target", payload);

        JsonElement frozen = tgtDoc.RootElement.Freeze();

        Assert.Equal("sourceValue", frozen.GetProperty("target").GetProperty("inner").GetString());
        Assert.Equal(99, frozen.GetProperty("target").GetProperty("count").GetInt32());
    }

    [Fact]
    public static void FreezeCrossDocumentArrayItem()
    {
        // Assign an array item from one mutable document into another,
        // then freeze the target.
        using var workspace = JsonWorkspace.Create();

        using var srcParsed = ParsedJsonDocument<JsonElement>.Parse("""[{"key":"val"}]""");
        using JsonDocumentBuilder<JsonElement.Mutable> srcDoc =
            srcParsed.RootElement.CreateBuilder(workspace);

        using var tgtParsed = ParsedJsonDocument<JsonElement>.Parse("[null]");
        using JsonDocumentBuilder<JsonElement.Mutable> tgtDoc =
            tgtParsed.RootElement.CreateBuilder(workspace);

        // Copy the object at index 0 from src into tgt at index 0.
        tgtDoc.RootElement.SetItem(0, srcDoc.RootElement[0]);

        JsonElement frozen = tgtDoc.RootElement.Freeze();

        Assert.Equal("val", frozen[0].GetProperty("key").GetString());
    }

    [Fact]
    public static void FreezeTwoLevelDeepCrossDocumentAssignment()
    {
        // Three mutable documents: A -> B -> C.
        // Assign a value from A into B, then assign that value from B into C,
        // then freeze C and verify the deeply-transferred value.
        using var workspace = JsonWorkspace.Create();

        using var parsedA = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":{"nested":{"deep":"fromA"}}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> docA =
            parsedA.RootElement.CreateBuilder(workspace);

        using var parsedB = ParsedJsonDocument<JsonElement>.Parse("""{"b":null}""");
        using JsonDocumentBuilder<JsonElement.Mutable> docB =
            parsedB.RootElement.CreateBuilder(workspace);

        using var parsedC = ParsedJsonDocument<JsonElement>.Parse("""{"c":null}""");
        using JsonDocumentBuilder<JsonElement.Mutable> docC =
            parsedC.RootElement.CreateBuilder(workspace);

        // Level 1: assign A's nested object into B.
        docB.RootElement.SetProperty("b", docA.RootElement.GetProperty("a"));

        // Level 2: assign B's "b" (which came from A) into C.
        docC.RootElement.SetProperty("c", docB.RootElement.GetProperty("b"));

        JsonElement frozen = docC.RootElement.Freeze();

        Assert.Equal("fromA", frozen.GetProperty("c").GetProperty("nested").GetProperty("deep").GetString());
    }

    [Fact]
    public static void FreezeTwoLevelDeepCrossDocumentArrayAssignment()
    {
        // Three mutable documents with arrays: A -> B -> C.
        using var workspace = JsonWorkspace.Create();

        using var parsedA = ParsedJsonDocument<JsonElement>.Parse("""[[1, 2, 3]]""");
        using JsonDocumentBuilder<JsonElement.Mutable> docA =
            parsedA.RootElement.CreateBuilder(workspace);

        using var parsedB = ParsedJsonDocument<JsonElement>.Parse("[null]");
        using JsonDocumentBuilder<JsonElement.Mutable> docB =
            parsedB.RootElement.CreateBuilder(workspace);

        using var parsedC = ParsedJsonDocument<JsonElement>.Parse("[null]");
        using JsonDocumentBuilder<JsonElement.Mutable> docC =
            parsedC.RootElement.CreateBuilder(workspace);

        // Level 1: assign A[0] (the inner array) into B[0].
        docB.RootElement.SetItem(0, docA.RootElement[0]);

        // Level 2: assign B[0] (which came from A) into C[0].
        docC.RootElement.SetItem(0, docB.RootElement[0]);

        JsonElement frozen = docC.RootElement.Freeze();

        Assert.Equal(1, frozen[0][0].GetInt32());
        Assert.Equal(2, frozen[0][1].GetInt32());
        Assert.Equal(3, frozen[0][2].GetInt32());
    }

    [Fact]
    public static void FreezeMixedCrossDocumentObjectAndArrayAssignment()
    {
        // Assign an object property from A into B's array, then assign
        // that array element from B into C's property, then freeze.
        using var workspace = JsonWorkspace.Create();

        using var parsedA = ParsedJsonDocument<JsonElement>.Parse(
            """{"data":{"x":42,"y":"hello"}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> docA =
            parsedA.RootElement.CreateBuilder(workspace);

        using var parsedB = ParsedJsonDocument<JsonElement>.Parse("[null, null]");
        using JsonDocumentBuilder<JsonElement.Mutable> docB =
            parsedB.RootElement.CreateBuilder(workspace);

        using var parsedC = ParsedJsonDocument<JsonElement>.Parse("""{"result":null}""");
        using JsonDocumentBuilder<JsonElement.Mutable> docC =
            parsedC.RootElement.CreateBuilder(workspace);

        // A's object -> B's array item.
        docB.RootElement.SetItem(0, docA.RootElement.GetProperty("data"));

        // B's array item (from A) -> C's property.
        docC.RootElement.SetProperty("result", docB.RootElement[0]);

        JsonElement frozen = docC.RootElement.Freeze();

        Assert.Equal(42, frozen.GetProperty("result").GetProperty("x").GetInt32());
        Assert.Equal("hello", frozen.GetProperty("result").GetProperty("y").GetString());
    }

    private static void FreezeAtInner(string innerJson, JsonValueKind valueType)
    {
        string json = $$"""{"obj":[{"not target":true,"target":{{innerJson}}}  ,5]}""";

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

    private static IJsonDocument SniffParentDocument<TElement>(TElement element)
        where TElement : struct, IJsonElement<TElement>
    {
        return element.ParentDocument;
    }
}