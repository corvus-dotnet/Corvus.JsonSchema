using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

[TestClass]
public class KeywordTests
{
    private static bool Eval(string schema, string instance, JsonSchemaEvaluatorOptions? options = null)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, options);
        return evaluator.Evaluate(instance);
    }

    [TestMethod]
    [DataRow("1", true)]
    [DataRow("1.0", true)]
    [DataRow("1e2", true)]
    [DataRow("1.5", false)]
    [DataRow("1e-2", false)]
    [DataRow("\"1\"", false)]
    public void IntegerType(string instance, bool expected)
    {
        Assert.AreEqual(expected, Eval("""{"type": "integer"}""", instance));
    }

    [TestMethod]
    public void NumericBoundsUseDecimalComparison()
    {
        Assert.IsTrue(Eval("""{"maximum": 12345678901234567890.5}""", "12345678901234567890"));
        Assert.IsFalse(Eval("""{"maximum": 12345678901234567890.5}""", "12345678901234567891"));
        Assert.IsTrue(Eval("""{"multipleOf": 0.0001}""", "0.0075"));
        Assert.IsFalse(Eval("""{"multipleOf": 0.0001}""", "0.00751"));
        Assert.IsTrue(Eval("""{"multipleOf": 1e-8}""", "12391239123"));
    }

    [TestMethod]
    public void StringLengthCountsCodePoints()
    {
        Assert.IsTrue(Eval("""{"maxLength": 2}""", "\"\\uD83D\\uDCA9\\uD83D\\uDCA9\""));
        Assert.IsFalse(Eval("""{"maxLength": 1}""", "\"\\uD83D\\uDCA9\\uD83D\\uDCA9\""));
        Assert.IsTrue(Eval("""{"minLength": 2}""", "\"é1\""));
    }

    [TestMethod]
    public void PatternUsesEcma262Semantics()
    {
        Assert.IsTrue(Eval("""{"pattern": "^\\d+$"}""", "\"123\""));
        Assert.IsFalse(Eval("""{"pattern": "^\\d+$"}""", "\"١٢٣\""), "non-ASCII digits are not \\d in ECMA-262");
        Assert.IsTrue(Eval("""{"pattern": "^abc"}""", "\"abcdef\""));
        Assert.IsFalse(Eval("""{"pattern": "^abc"}""", "\"xabc\""));
        Assert.IsTrue(Eval("""{"pattern": "^.{2,4}$"}""", "\"abc\""));
        Assert.IsFalse(Eval("""{"pattern": "^.{2,4}$"}""", "\"abcde\""));
    }

    [TestMethod]
    public void EnumAndConst()
    {
        Assert.IsTrue(Eval("""{"enum": ["a", "b"]}""", "\"b\""));
        Assert.IsFalse(Eval("""{"enum": ["a", "b"]}""", "\"c\""));
        Assert.IsTrue(Eval("""{"enum": [1, {"x": [1, 2]}, null]}""", """{"x": [1, 2]}"""));
        Assert.IsFalse(Eval("""{"enum": [1, {"x": [1, 2]}, null]}""", """{"x": [2, 1]}"""));
        Assert.IsTrue(Eval("""{"const": 1.0}""", "1"));
        Assert.IsTrue(Eval("""{"const": {"a": [1, "b"]}}""", """{"a": [1, "b"]}"""));
        Assert.IsFalse(Eval("""{"const": "a"}""", "\"b\""));
    }

    [TestMethod]
    public void ObjectKeywordsInOnePass()
    {
        const string schema = """
            {
              "properties": {"a": {"type": "integer"}, "b": true},
              "patternProperties": {"^x": {"type": "string"}},
              "additionalProperties": {"type": "boolean"},
              "required": ["a"],
              "dependentRequired": {"b": ["c"]},
              "dependentSchemas": {"c": {"properties": {"d": {"type": "null"}}}},
              "propertyNames": {"maxLength": 3},
              "minProperties": 1,
              "maxProperties": 6
            }
            """;
        Assert.IsTrue(Eval(schema, """{"a": 1}"""));
        Assert.IsFalse(Eval(schema, """{"a": "1"}"""));
        Assert.IsFalse(Eval(schema, """{"b": 1}"""), "required a");
        Assert.IsTrue(Eval(schema, """{"a": 1, "xy": "s"}"""));
        Assert.IsFalse(Eval(schema, """{"a": 1, "xy": 1}"""));
        Assert.IsTrue(Eval(schema, """{"a": 1, "z": true}"""));
        Assert.IsFalse(Eval(schema, """{"a": 1, "z": 1}"""));
        Assert.IsFalse(Eval(schema, """{"a": 1, "b": 1}"""), "b requires c");
        Assert.IsTrue(Eval(schema, """{"a": 1, "b": 1, "c": true}"""));
        Assert.IsFalse(Eval(schema, """{"a": 1, "c": true, "d": 1}"""), "dependent schema");
        Assert.IsFalse(Eval(schema, """{"a": 1, "long": true}"""), "propertyNames");
        Assert.IsFalse(Eval(schema, "{}"), "minProperties");
    }

    [TestMethod]
    public void ArrayKeywordsInOnePass()
    {
        const string schema = """
            {
              "prefixItems": [{"type": "integer"}, {"type": "string"}],
              "items": {"type": "boolean"},
              "contains": {"const": true},
              "minContains": 1,
              "maxContains": 2,
              "uniqueItems": true,
              "minItems": 1,
              "maxItems": 5
            }
            """;
        Assert.IsTrue(Eval(schema, """[1, "a", true]"""));
        Assert.IsFalse(Eval(schema, """[1, "a", 1]"""), "items");
        Assert.IsFalse(Eval(schema, """["a", "a", true]"""), "prefixItems");
        Assert.IsFalse(Eval(schema, """[1, "a", false]"""), "contains");
        Assert.IsFalse(Eval(schema, """[1, "a", true, true, true]"""), "maxContains and uniqueItems");
        Assert.IsFalse(Eval(schema, """[1, "a", true, false, false]"""), "uniqueItems");
        Assert.IsFalse(Eval(schema, "[]"), "minItems");
    }

    [TestMethod]
    public void UnevaluatedItemsAndProperties()
    {
        const string schema = """
            {
              "allOf": [{"properties": {"a": true}}, {"patternProperties": {"^b": true}}],
              "anyOf": [{"required": ["c"], "properties": {"c": true}}, {"required": ["d"], "properties": {"d": true}}],
              "if": {"required": ["e"], "properties": {"e": {"type": "integer"}}},
              "then": {"properties": {"f": true}},
              "unevaluatedProperties": false
            }
            """;
        Assert.IsTrue(Eval(schema, """{"a": 1, "b1": 2, "c": 3}"""));
        Assert.IsTrue(Eval(schema, """{"c": 3, "d": 4}"""));
        Assert.IsFalse(Eval(schema, """{"c": 3, "x": 4}"""));
        Assert.IsTrue(Eval(schema, """{"c": 3, "e": 1, "f": 2}"""));
        Assert.IsFalse(Eval(schema, """{"c": 3, "e": "s", "f": 2}"""), "if fails so then does not apply and f is unevaluated");
        Assert.IsFalse(Eval(schema, """{"c": 3, "e": "s"}"""), "if fails, e unevaluated");

        const string items = """
            {
              "prefixItems": [true],
              "contains": {"type": "string"},
              "unevaluatedItems": {"type": "integer"}
            }
            """;
        Assert.IsTrue(Eval(items, """["x", 1, "y", 2]"""));
        Assert.IsFalse(Eval(items, """["x", 1, "y", 2.5]"""));
    }

    [TestMethod]
    public void FormatIsAnnotationByDefaultAndAssertionOnRequest()
    {
        const string schema = """{"format": "ipv4"}""";
        Assert.IsTrue(Eval(schema, "\"not an ip\""));
        var assert = new JsonSchemaEvaluatorOptions { AssertFormat = true };
        Assert.IsFalse(Eval(schema, "\"not an ip\"", assert));
        Assert.IsTrue(Eval(schema, "\"127.0.0.1\"", assert));
    }

    [TestMethod]
    public void BooleanSchemas()
    {
        Assert.IsTrue(Eval("true", "1"));
        Assert.IsFalse(Eval("false", "1"));
        Assert.IsTrue(Eval("{}", "[1, {}]"));
        Assert.IsFalse(Eval("""{"properties": {"a": false}}""", """{"a": 1}"""));
        Assert.IsTrue(Eval("""{"properties": {"a": false}}""", """{"b": 1}"""));
    }

    [TestMethod]
    public void RunawayRecursionIsDetected()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"allOf": [{"$ref": "#"}]}""");
        Assert.ThrowsExactly<JsonSchemaEvaluationException>(() => evaluator.Evaluate("1"));
    }
}
