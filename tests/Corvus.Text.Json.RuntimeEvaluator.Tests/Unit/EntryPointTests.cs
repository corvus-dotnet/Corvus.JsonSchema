using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Evaluation rooted at an arbitrary subschema, as the generated models allow (e.g. "schema.json#/$defs/PersonArray").
/// </summary>
[TestClass]
public class EntryPointTests
{
    private const string Document = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/people",
          "type": "object",
          "properties": {"people": {"$ref": "#/$defs/PersonArray"}},
          "$defs": {
            "PersonArray": {"type": "array", "items": {"$ref": "#/$defs/Person"}},
            "Person": {
              "type": "object",
              "required": ["name"],
              "properties": {"name": {"$ref": "#name"}, "manager": {"$ref": "#"}},
              "additionalProperties": false
            },
            "Name": {"$anchor": "name", "type": "string", "minLength": 1},
            "Tree": {
              "$id": "tree",
              "$dynamicAnchor": "node",
              "type": "object",
              "properties": {"data": true, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}}
            },
            "StrictTree": {
              "$id": "strict-tree",
              "$dynamicAnchor": "node",
              "$ref": "tree",
              "unevaluatedProperties": false
            }
          }
        }
        """;

    private static readonly byte[] DocumentBytes = Encoding.UTF8.GetBytes(Document);

    [TestMethod]
    public void PointerEntryPoint()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DocumentBytes, "#/$defs/PersonArray");
        Assert.IsTrue(evaluator.Evaluate("""[{"name": "Ada"}, {"name": "Grace"}]"""));
        Assert.IsFalse(evaluator.Evaluate("""[{"name": ""}]"""));
        Assert.IsFalse(evaluator.Evaluate("""[{"nom": "Ada"}]"""));
        Assert.IsFalse(evaluator.Evaluate("""{"people": []}"""), "the entry point is the array, not the document root");
    }

    [TestMethod]
    public void RefToDocumentRootFromEntryPoint()
    {
        // "#" inside Person refers to the document root (the people object), not the entry point.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DocumentBytes, "#/$defs/Person");
        Assert.IsTrue(evaluator.Evaluate("""{"name": "Ada", "manager": {"people": [{"name": "Grace"}]}}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"name": "Ada", "manager": {"people": [{"name": 1}]}}"""));
    }

    [TestMethod]
    public void AnchorEntryPoint()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DocumentBytes, new JsonSchemaEvaluatorOptions { EntryPoint = "#name" });
        Assert.IsTrue(evaluator.Evaluate("\"x\""));
        Assert.IsFalse(evaluator.Evaluate("\"\""));
    }

    [TestMethod]
    public void EmbeddedResourceEntryPointStartsTheDynamicScope()
    {
        // Rooting at strict-tree makes it the outermost resource, so every $dynamicRef resolves to it.
        using JsonSchemaEvaluator strict = JsonSchemaEvaluator.Compile(DocumentBytes, "strict-tree");
        Assert.IsTrue(strict.UsesDynamicScope);
        Assert.IsTrue(strict.Evaluate("""{"data": 1, "children": [{"data": 2}]}"""));
        Assert.IsFalse(strict.Evaluate("""{"data": 1, "children": [{"daat": 2}]}"""));

        // Rooting at tree leaves strict-tree out of scope, so misspelled properties are allowed.
        using JsonSchemaEvaluator tree = JsonSchemaEvaluator.Compile(DocumentBytes, "https://example.com/tree");
        Assert.IsTrue(tree.Evaluate("""{"data": 1, "children": [{"daat": 2}]}"""));
    }

    [TestMethod]
    public void UnresolvableEntryPointThrows()
    {
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => JsonSchemaEvaluator.Compile(DocumentBytes, "#/$defs/Missing"));
    }
}
