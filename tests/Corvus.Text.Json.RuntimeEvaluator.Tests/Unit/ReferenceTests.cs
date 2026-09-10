using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

[TestClass]
public class ReferenceTests
{
    private static bool Eval(string schema, string instance, JsonSchemaEvaluatorOptions? options = null)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, options);
        return evaluator.Evaluate(instance);
    }

    [TestMethod]
    public void RootPointerRef()
    {
        const string schema = """{"properties": {"foo": {"$ref": "#"}}, "additionalProperties": false}""";
        Assert.IsTrue(Eval(schema, """{"foo": {"foo": false}}"""));
        Assert.IsFalse(Eval(schema, """{"foo": {"bar": false}}"""));
    }

    [TestMethod]
    public void RefToDefs()
    {
        const string schema = """{"$ref": "#/$defs/a", "$defs": {"a": {"type": "integer"}}}""";
        Assert.IsTrue(Eval(schema, "1"));
        Assert.IsFalse(Eval(schema, "\"a\""));
    }

    [TestMethod]
    public void Draft7RefIgnoresSiblings()
    {
        const string schema = """{"$ref": "#/definitions/a", "type": "string", "definitions": {"a": {"type": "integer"}}}""";
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.Draft7 };
        Assert.IsTrue(Eval(schema, "1", options));
        Assert.IsFalse(Eval(schema, "\"a\"", options));
    }

    [TestMethod]
    public void Draft201909RefAppliesSiblings()
    {
        const string schema = """{"$schema": "https://json-schema.org/draft/2019-09/schema", "$ref": "#/$defs/a", "maximum": 5, "$defs": {"a": {"type": "integer"}}}""";
        Assert.IsTrue(Eval(schema, "1"));
        Assert.IsFalse(Eval(schema, "10"));
        Assert.IsFalse(Eval(schema, "\"a\""));
    }

    [TestMethod]
    public void EmbeddedResourceWithRelativeRef()
    {
        const string schema = """
            {
              "$id": "http://example.com/root.json",
              "properties": {"a": {"$ref": "inner.json#/$defs/x"}},
              "$defs": {"inner": {"$id": "inner.json", "$defs": {"x": {"type": "string"}}}}
            }
            """;
        Assert.IsTrue(Eval(schema, """{"a": "s"}"""));
        Assert.IsFalse(Eval(schema, """{"a": 1}"""));
    }

    [TestMethod]
    public void AnchorRef()
    {
        const string schema = """{"$ref": "#foo", "$defs": {"A": {"$anchor": "foo", "type": "integer"}}}""";
        Assert.IsTrue(Eval(schema, "1"));
        Assert.IsFalse(Eval(schema, "\"a\""));
    }

    [TestMethod]
    public void Draft4LocationIndependentId()
    {
        const string schema = """{"allOf": [{"$ref": "#foo"}], "definitions": {"A": {"id": "#foo", "type": "integer"}}}""";
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.Draft4 };
        Assert.IsTrue(Eval(schema, "1", options));
        Assert.IsFalse(Eval(schema, "\"a\"", options));
    }

    [TestMethod]
    public void DynamicRefStrictTree()
    {
        // The canonical tree / strict-tree example from the 2020-12 specification.
        const string strictTree = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "http://localhost:1234/draft2020-12/strict-tree.json",
              "$dynamicAnchor": "node",
              "$ref": "tree.json",
              "unevaluatedProperties": false
            }
            """;
        var options = new JsonSchemaEvaluatorOptions { DocumentResolver = Tests.Suite.SuiteRunner.ResolveRemote };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(strictTree, options);

        // The entry resource defines the anchor, so every $dynamicRef resolves to it on every path and the compiler
        // makes the reference static; the strict semantics hold without a dynamic scope.
        Assert.IsFalse(evaluator.UsesDynamicScope);
        Assert.IsFalse(evaluator.Evaluate("""{"children": [{"daat": 1}]}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"children": [{"data": 1}]}"""));
    }

    [TestMethod]
    public void DynamicRefWithSingleDefinerIsStatic()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$dynamicAnchor": "expr",
              "anyOf": [{"type": "integer"}, {"type": "array", "items": {"$dynamicRef": "#expr"}}]
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsFalse(evaluator.UsesDynamicScope);
        Assert.IsTrue(evaluator.Evaluate("[1, [2, [3]]]"));
        Assert.IsFalse(evaluator.Evaluate("[1, [2, [\"x\"]]]"));
    }

    [TestMethod]
    public void RecursiveRefNesting()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2019-09/schema",
              "$id": "http://localhost:4242/recursiveRef3/schema.json",
              "$recursiveAnchor": true,
              "$defs": {
                "myobject": {
                  "$id": "myobject.json",
                  "$recursiveAnchor": true,
                  "anyOf": [
                    {"type": "string"},
                    {"type": "object", "additionalProperties": {"$recursiveRef": "#"}}
                  ]
                }
              },
              "anyOf": [{"type": "integer"}, {"$ref": "#/$defs/myobject"}]
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);

        // The root carries $recursiveAnchor, so the reference is resolved to it statically.
        Assert.IsFalse(evaluator.UsesDynamicScope);
        Assert.IsTrue(evaluator.Evaluate("1"));
        Assert.IsTrue(evaluator.Evaluate("""{"foo": "bar"}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"foo": 1}"""), "integer is allowed because the outermost resource with $recursiveAnchor is the root");
        Assert.IsFalse(evaluator.Evaluate("""{"foo": [1]}"""));
    }

    [TestMethod]
    public void RefCreatesNewUnevaluatedScope()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2019-09/schema",
              "$defs": {"A": {"unevaluatedProperties": false}},
              "properties": {"prop1": {"type": "string"}},
              "$ref": "#/$defs/A"
            }
            """;
        Assert.IsFalse(Eval(schema, """{"prop1": "match"}"""));
        Assert.IsTrue(Eval(schema, "{}"));
    }

    [TestMethod]
    public void UnevaluatedPropertiesSeesRefAnnotations()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$ref": "#/$defs/A",
              "unevaluatedProperties": false,
              "$defs": {"A": {"properties": {"a": true}}}
            }
            """;
        Assert.IsTrue(Eval(schema, """{"a": 1}"""));
        Assert.IsFalse(Eval(schema, """{"b": 1}"""));
    }

    [TestMethod]
    public void UnresolvableRefThrowsAtCompile()
    {
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => JsonSchemaEvaluator.Compile("""{"$ref": "#/$defs/missing"}"""));
    }
}
