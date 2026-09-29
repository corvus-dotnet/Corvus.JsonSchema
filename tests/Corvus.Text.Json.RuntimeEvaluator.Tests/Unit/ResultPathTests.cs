// <copyright file="ResultPathTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Results paths follow the generated-model conventions: a pure <c>$ref</c> is elided (its target is reported
/// through the parent's context with <c>$ref</c> appended to the evaluation path), and an entry point rooted at a
/// subschema reports that subschema's location as the root schema location.
/// </summary>
[TestClass]
public class ResultPathTests
{
    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$defs": {
            "fooId": { "type": "integer", "minimum": 0 },
            "holder": {
              "type": "object",
              "properties": { "fooId": { "$ref": "#/$defs/fooId" } }
            },
            "viaRef": { "$ref": "#/$defs/fooId" }
          }
        }
        """;

    [TestMethod]
    public void EntryPointRootReportsItsOwnSchemaLocation()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Encoding.UTF8.GetBytes(Schema), "#/$defs/fooId");
        Assert.AreEqual(
            """
            fail|/$defs/fooId|||The value was expected to match the subschema.
            fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'
            """.Replace("\r\n", "\n"),
            Dump(evaluator, "\"notAnInteger\"", JsonSchemaResultsLevel.Detailed));
    }

    [TestMethod]
    public void PureRefPropertyIsElidedWithRefInEvaluationPath()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Encoding.UTF8.GetBytes(Schema), "#/$defs/holder");
        Assert.AreEqual(
            """
            fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.
            fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'
            fail|/$defs/holder|||The value was expected to match the subschema.
            """.Replace("\r\n", "\n"),
            Dump(evaluator, """{ "fooId": "notAnInteger" }""", JsonSchemaResultsLevel.Detailed));
    }

    [TestMethod]
    public void PureRefRootReportsAgainstItsTarget()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Encoding.UTF8.GetBytes(Schema), "#/$defs/viaRef");
        Assert.AreEqual(
            """
            fail|/$defs/fooId|||The value was expected to match the subschema.
            fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'
            """.Replace("\r\n", "\n"),
            Dump(evaluator, "\"notAnInteger\"", JsonSchemaResultsLevel.Detailed));
    }

    [TestMethod]
    public void RequiredFailureCarriesThePropertyName()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"type": "object", "required": ["name"]}""");
        string dump = Dump(evaluator, "{}", JsonSchemaResultsLevel.Detailed);
        StringAssert.Contains(dump, "required");
        StringAssert.Contains(dump, "'name'");
    }

    [TestMethod]
    public void DuplicatedSubschemaReportsItsOwnSchemaLocation()
    {
        // Identical subschemas share one node in flag mode; results must still report each one's own location.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string"}, "b": {"type": "string"}}}""");
        Assert.AreEqual(
            """
            fail|/properties/b|/properties/b|/b|The value was expected to match the subschema.
            fail|/properties/b/type|/properties/b/type|/b|The value was expected to be of type 'string'
            fail||||The value was expected to match the subschema.
            """.Replace("\r\n", "\n"),
            Dump(evaluator, """{"b": 1}""", JsonSchemaResultsLevel.Detailed));
        Assert.AreEqual(Dump(evaluator, """{"b": 1}""", JsonSchemaResultsLevel.Detailed), Dump(RoundTrip(evaluator), """{"b": 1}""", JsonSchemaResultsLevel.Detailed));
    }

    [TestMethod]
    public void DependenciesReportsUnderItsOwnKeyword()
    {
        // The draft 4 to 7 keyword is still "dependencies" when a 2019-09 or 2020-12 schema uses it.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "dependencies": {"a": ["b"], "c": {"required": ["d"]}}}""");
        string expected = """
            fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.
            fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'
            fail||||The value was expected to match the subschema.
            fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property 'c'
            fail|/dependencies|/dependencies|/b|Required property not present 'b'
            """.Replace("\r\n", "\n");
        Assert.AreEqual(expected, Dump(evaluator, """{"a": 1, "c": 1}""", JsonSchemaResultsLevel.Detailed));
        Assert.AreEqual(expected, Dump(RoundTrip(evaluator), """{"a": 1, "c": 1}""", JsonSchemaResultsLevel.Detailed));

        using JsonSchemaEvaluator modern = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "dependentRequired": {"a": ["b"]}}""");
        StringAssert.Contains(Dump(modern, """{"a": 1}""", JsonSchemaResultsLevel.Detailed), "fail|/dependentRequired|/dependentRequired|/b|");
    }

    [TestMethod]
    public void StaticDynamicRefHopIsNamedInTheEvaluationPath()
    {
        // A $dynamicRef that resolves statically is elided like $ref, but the hop keeps its own keyword.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "properties": {"p": {"$dynamicRef": "#/$defs/n"}}, "$defs": {"n": {"type": "integer"}}}""");
        Assert.AreEqual(
            """
            fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.
            fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type 'integer'
            fail||||The value was expected to match the subschema.
            """.Replace("\r\n", "\n"),
            Dump(evaluator, """{"p": "x"}""", JsonSchemaResultsLevel.Detailed));
    }

    [TestMethod]
    [DataRow("""{"$schema": "https://json-schema.org/draft/2020-12/schema", "$defs": {"a": {"minimum": 5}, "b": {"maximum": 10}}, "$ref": "#/$defs/a", "$dynamicRef": "#/$defs/b"}""", DisplayName = "$ref first")]
    [DataRow("""{"$schema": "https://json-schema.org/draft/2020-12/schema", "$defs": {"a": {"minimum": 5}, "b": {"maximum": 10}}, "$dynamicRef": "#/$defs/b", "$ref": "#/$defs/a"}""", DisplayName = "$dynamicRef first")]
    [DataRow("""{"$schema": "https://json-schema.org/draft/2020-12/schema", "$defs": {"a": {"minimum": 5}, "b": {"$dynamicAnchor": "b", "maximum": 10}}, "$ref": "#/$defs/a", "$dynamicRef": "#b"}""", DisplayName = "demoted anchor")]
    public void StaticDynamicRefDoesNotReplaceASiblingRef(string schema)
    {
        // In 2020-12 $ref and $dynamicRef are both applicators; a statically resolved $dynamicRef must not drop the $ref.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        foreach (JsonSchemaEvaluator e in new[] { evaluator, RoundTrip(evaluator) })
        {
            Assert.IsFalse(e.Evaluate("1"), "minimum from $ref");
            Assert.IsTrue(e.Evaluate("7"));
            Assert.IsFalse(e.Evaluate("11"), "maximum from $dynamicRef");
            using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
            Assert.IsFalse(e.Evaluate("1", collector));
        }

        StringAssert.Contains(Dump(evaluator, "1", JsonSchemaResultsLevel.Detailed), "fail|/$defs/a/minimum|/$ref/minimum||");
        StringAssert.Contains(Dump(evaluator, "11", JsonSchemaResultsLevel.Detailed), "fail|/$defs/b/maximum|/$dynamicRef/maximum||");
    }

    private static JsonSchemaEvaluator RoundTrip(JsonSchemaEvaluator evaluator) => JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());

    private static string Dump(JsonSchemaEvaluator evaluator, string instance, JsonSchemaResultsLevel level)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(level);
        evaluator.Evaluate(doc.RootElement, collector);

        StringBuilder builder = new();
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            builder
                .Append(result.IsMatch ? "match" : "fail").Append('|')
                .Append(result.GetSchemaEvaluationLocationText()).Append('|')
                .Append(result.GetEvaluationLocationText()).Append('|')
                .Append(result.GetDocumentEvaluationLocationText()).Append('|')
                .Append(result.GetMessageText()).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }
}