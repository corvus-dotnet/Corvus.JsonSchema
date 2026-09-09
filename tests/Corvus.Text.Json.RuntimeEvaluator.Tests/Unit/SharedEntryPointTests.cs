using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Many entry points over one loaded document set, sharing compiled subschemas.
/// </summary>
[TestClass]
public class SharedEntryPointTests
{
    private const string Document = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/catalog",
          "type": "object",
          "properties": {"items": {"type": "array", "items": {"$ref": "#/$defs/Item"}}},
          "$defs": {
            "Item": {"type": "object", "required": ["sku"], "properties": {"sku": {"$ref": "#/$defs/Sku"}, "price": {"$ref": "#/$defs/Price"}}},
            "Sku": {"type": "string", "pattern": "^[A-Z]{3}-[0-9]{4}$"},
            "Price": {"type": "number", "minimum": 0},
            "Unreachable": {"type": "integer", "multipleOf": 7}
          }
        }
        """;

    [TestMethod]
    public void EntryPointsShareNodesAndCompileOnlyWhatIsNew()
    {
        using JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile(Document);
        int rootNodes = root.NodeCount;

        using JsonSchemaEvaluator item = root.ForEntryPoint("#/$defs/Item");
        Assert.AreEqual(rootNodes, item.NodeCount, "Item was already reachable from the root: nothing new to compile");
        Assert.IsTrue(item.Evaluate("""{"sku": "ABC-1234", "price": 1.5}"""));
        Assert.IsFalse(item.Evaluate("""{"sku": "abc-1234"}"""));

        using JsonSchemaEvaluator unreachable = root.ForEntryPoint("#/$defs/Unreachable");
        Assert.AreEqual(rootNodes + 1, unreachable.NodeCount, "exactly one new subschema");
        Assert.IsTrue(unreachable.Evaluate("14"));
        Assert.IsFalse(unreachable.Evaluate("15"));

        // The original evaluator still works and sees the same (grown) graph.
        Assert.IsTrue(root.Evaluate("""{"items": [{"sku": "ABC-1234"}]}"""));
        Assert.IsFalse(root.Evaluate("""{"items": [{"price": 1}]}"""));
    }

    [TestMethod]
    public void SharedDocumentsSurviveDisposalOfTheFirstEvaluator()
    {
        JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile(Document);
        JsonSchemaEvaluator price = root.ForEntryPoint("#/$defs/Price");
        root.Dispose();
        Assert.IsTrue(price.Evaluate("0"));
        Assert.IsFalse(price.Evaluate("-1"));
        price.Dispose();
    }

    [TestMethod]
    public void LaterEntryPointCanPromoteADynamicReference()
    {
        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["https://t.example/tree"] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/tree",
                  "$dynamicAnchor": "node",
                  "type": "object",
                  "properties": {"data": true, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}}
                }
                """),
            ["https://t.example/strict-tree"] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/strict-tree",
                  "$dynamicAnchor": "node",
                  "$ref": "tree",
                  "unevaluatedProperties": false
                }
                """),
        };

        bool Resolve(string uri, out ReadOnlyMemory<byte> utf8)
        {
            if (documents.TryGetValue(uri, out byte[]? bytes))
            {
                utf8 = bytes;
                return true;
            }

            utf8 = default;
            return false;
        }

        // tree alone defines $dynamicAnchor "node", so its $dynamicRef is static and any object is a node.
        var options = new JsonSchemaEvaluatorOptions { DocumentResolver = Resolve };
        using JsonSchemaEvaluator treeEvaluator = JsonSchemaEvaluator.Compile(documents["https://t.example/tree"], options);
        Assert.IsFalse(treeEvaluator.UsesDynamicScope);
        Assert.IsTrue(treeEvaluator.Evaluate("""{"data": 1, "children": [{"daat": 2}]}"""));

        // Adding strict-tree as an entry point loads a second resource defining "node": the reference becomes
        // dynamic, and evaluation rooted at strict-tree rejects the misspelling while tree still accepts it.
        using JsonSchemaEvaluator strict = treeEvaluator.ForEntryPoint("https://t.example/strict-tree");
        Assert.IsTrue(strict.UsesDynamicScope);
        Assert.IsFalse(strict.Evaluate("""{"data": 1, "children": [{"daat": 2}]}"""));
        Assert.IsTrue(strict.Evaluate("""{"data": 1, "children": [{"data": 2}]}"""));
        Assert.IsTrue(treeEvaluator.Evaluate("""{"data": 1, "children": [{"daat": 2}]}"""), "rooted at tree, strict-tree is not in scope");
    }

    [TestMethod]
    public void EntryPointAfterDisposalThrows()
    {
        JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile(Document);
        root.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => root.ForEntryPoint("#/$defs/Price"));
    }
}
