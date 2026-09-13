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