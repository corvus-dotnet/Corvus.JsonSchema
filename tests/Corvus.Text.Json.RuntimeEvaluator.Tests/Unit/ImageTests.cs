// <copyright file="ImageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A program image reproduces the compiled program: the same verdicts, the same verbose results and annotations,
/// and the same entry points.
/// </summary>
[TestClass]
public class ImageTests
{
    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/image-test",
          "title": "Image test",
          "type": "object",
          "properties": {
            "name": { "type": "string", "minLength": 1, "pattern": "^[a-z]+$", "description": "The name" },
            "age": { "type": "integer", "minimum": 0, "maximum": 150, "multipleOf": 1 },
            "kind": { "enum": ["a", "b", "c"] },
            "shape": { "const": { "sides": 4, "tags": ["x", "y"] } },
            "mixed": { "enum": [1, "two", null, true, { "k": "v" }, [1, 2]] },
            "when": { "type": "string", "format": "date-time" },
            "items": { "type": "array", "prefixItems": [{ "type": "string" }], "items": { "$ref": "#/$defs/item" }, "uniqueItems": true, "unevaluatedItems": false },
            "choice": { "oneOf": [{ "$ref": "#/$defs/left" }, { "$ref": "#/$defs/right" }] },
            "tree": { "$dynamicRef": "#node" }
          },
          "patternProperties": { "^x-": { "type": "number" } },
          "additionalProperties": false,
          "required": ["name"],
          "dependentRequired": { "age": ["kind"] },
          "dependentSchemas": { "kind": { "properties": { "kindNote": { "type": "string" } } } },
          "unevaluatedProperties": false,
          "$defs": {
            "item": { "type": "object", "properties": { "id": { "type": "integer" } }, "required": ["id"] },
            "left": { "type": "object", "properties": { "type": { "const": "left" }, "l": { "type": "string" } }, "required": ["type"] },
            "right": { "type": "object", "properties": { "type": { "const": "right" }, "r": { "type": "string" } }, "required": ["type"] },
            "node": { "$dynamicAnchor": "node", "type": "object", "properties": { "children": { "type": "array", "items": { "$dynamicRef": "#node" } } } }
          }
        }
        """;

    private static readonly string[] Instances =
    [
        """{"name": "abc", "age": 3, "kind": "a", "shape": {"sides": 4, "tags": ["x", "y"]}, "mixed": {"k": "v"}, "when": "2020-01-01T00:00:00Z", "items": ["first", {"id": 1}], "choice": {"type": "left", "l": "v"}, "tree": {"children": [{"children": []}]}, "x-extra": 1}""",
        """{"name": "ABC"}""",
        """{"name": "abc", "age": -1}""",
        """{"name": "abc", "age": 3}""",
        """{"name": "abc", "kind": "d"}""",
        """{"name": "abc", "shape": {"sides": 4, "tags": ["x", "z"]}}""",
        """{"name": "abc", "mixed": [1, 2]}""",
        """{"name": "abc", "mixed": [1, 3]}""",
        """{"name": "abc", "items": ["first", {"id": 1}, {"id": 1}]}""",
        """{"name": "abc", "choice": {"type": "left", "r": 1}}""",
        """{"name": "abc", "tree": {"children": [{"children": [1]}]}}""",
        """{"name": "abc", "x-extra": "no"}""",
        """{"name": "abc", "other": 1}""",
        """{"name": "abc", "when": "nope"}""",
        """[]""",
    ];

    [TestMethod]
    public void ImageReproducesVerdictsResultsAndAnnotations()
    {
        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true };
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(Schema, options);
        byte[] image = compiled.ToProgramImage();
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image, options);

        Assert.AreEqual(compiled.NodeCount, loaded.NodeCount);
        Assert.AreEqual(compiled.UsesDynamicScope, loaded.UsesDynamicScope);

        int valid = 0;
        foreach (string instance in Instances)
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            bool expected = compiled.Evaluate(doc.RootElement);
            bool actual = loaded.Evaluate(doc.RootElement);
            Assert.AreEqual(expected, actual, instance);
            valid += expected ? 1 : 0;

            Assert.AreEqual(Dump(compiled, doc), Dump(loaded, doc), instance);
        }

        Assert.AreEqual(3, valid, "The first instance, the [1, 2] enum member and the one-of case are the valid ones.");
    }

    [TestMethod]
    public void ImageRecordsEntryPoints()
    {
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(Schema);
        using JsonSchemaEvaluator item = compiled.ForEntryPoint("#/$defs/item");
        byte[] image = compiled.ToProgramImage();

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image);
        using JsonSchemaEvaluator loadedItem = loaded.ForEntryPoint("#/$defs/item");
        Assert.IsTrue(loadedItem.Evaluate("""{"id": 1}"""));
        Assert.IsFalse(loadedItem.Evaluate("""{"id": "x"}"""));
        Assert.IsFalse(loaded.Evaluate("""{"id": 1}"""), "The root entry point requires 'name'.");

        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => loaded.ForEntryPoint("#/$defs/left"));
    }

    [TestMethod]
    public void ImageRejectsForeignBytes()
    {
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => JsonSchemaEvaluator.FromProgramImage(Encoding.UTF8.GetBytes("{\"type\": \"string\"}")));
    }

    [TestMethod]
    public void ImageIsSmallerThanTheSchemaTextForOrdinarySchemas()
    {
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(Schema);
        byte[] image = compiled.ToProgramImage();
        Assert.IsTrue(image.Length > 0);
        Console.WriteLine($"schema {Encoding.UTF8.GetByteCount(Schema)} bytes, image {image.Length} bytes, {compiled.NodeCount} nodes");
    }

    private static string Dump(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement> doc)
    {
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        evaluator.Evaluate(doc.RootElement, collector);
        StringBuilder text = new();
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            text.Append(result.IsMatch ? "match" : "fail").Append('|')
                .Append(result.GetSchemaEvaluationLocationText()).Append('|')
                .Append(result.GetEvaluationLocationText()).Append('|')
                .Append(result.GetDocumentEvaluationLocationText()).Append('|')
                .Append(result.GetMessageText()).Append('\n');
        }

        foreach (KeyValuePair<(string InstanceLocation, string Keyword), Dictionary<string, string>> entry in JsonSchemaAnnotationProducer.CollectAnnotations(collector))
        {
            foreach (KeyValuePair<string, string> schemaEntry in entry.Value)
            {
                text.Append("annotation|").Append(entry.Key.InstanceLocation).Append('|').Append(entry.Key.Keyword).Append('|').Append(schemaEntry.Key).Append('|').Append(schemaEntry.Value).Append('\n');
            }
        }

        return text.ToString();
    }

    [TestMethod]
    public void ImagePatternTableDrivesTheRegexProvider()
    {
        const string schema = """
            {
              "type": "object",
              "properties": {
                "code": { "type": "string", "pattern": "^[a-z]+[a-z0-9]{2,4}$" },
                "prefixed": { "type": "string", "pattern": "^abc" },
                "sized": { "type": "string", "pattern": "^.{2,5}$" }
              },
              "patternProperties": { "^x-[a-z]+[a-z0-9]*$": { "type": "number" }, "^[a-z]+[a-z0-9]{2,4}$": { "type": "string" } }
            }
            """;
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(schema);
        byte[] image = compiled.ToProgramImage();

        // Only the patterns that need a Regex are in the table, in first-seen node order (the root's patternProperties
        // come before the property subschemas), each once; prefixes and length ranges are matched without one.
        IReadOnlyList<string> patterns = JsonSchemaEvaluator.GetImagePatterns(image);
        CollectionAssert.AreEqual(new[] { "^x-[a-z]+[a-z0-9]*$", "^[a-z]+[a-z0-9]{2,4}$" }, patterns.ToArray());

        var asked = new List<(int Index, string Pattern)>();
        var options = new JsonSchemaEvaluatorOptions
        {
            RegexProvider = (index, pattern) =>
            {
                asked.Add((index, pattern));
                return new System.Text.RegularExpressions.Regex(JsonSchemaEvaluator.ToDotNetPattern(pattern), System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            },
        };
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image, options);

        CollectionAssert.AreEqual(new[] { (0, "^x-[a-z]+[a-z0-9]*$"), (1, "^[a-z]+[a-z0-9]{2,4}$") }, asked.ToArray(), "Each table pattern is requested once, by index.");

        foreach (string instance in new[]
        {
            """{"code": "ab-123", "prefixed": "abcdef", "sized": "abc", "x-rate": 1, "zz-99": "ok"}""",
            """{"code": "AB-123"}""",
            """{"prefixed": "xabc"}""",
            """{"sized": "abcdefgh"}""",
            """{"x-rate": "no"}""",
            """{"zz-99": 1}""",
        })
        {
            Assert.AreEqual(compiled.Evaluate(instance), loaded.Evaluate(instance), instance);
        }
    }

    [TestMethod]
    public void RegexProviderReturningNullFallsBackToConstruction()
    {
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile("""{"type": "string", "pattern": "^[0-9]+(?:.[0-9]+)?$"}""");
        byte[] image = compiled.ToProgramImage();
        int calls = 0;
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image, new JsonSchemaEvaluatorOptions { RegexProvider = (_, _) => { calls++; return null; } });
        Assert.AreEqual(1, calls);
        Assert.IsTrue(loaded.Evaluate("\"123\""));
        Assert.IsFalse(loaded.Evaluate("\"12a\""));
    }

    [TestMethod]
    public void RegexProviderIsOfferedCompiledPatternsWithNoIndex()
    {
        var asked = new List<int>();
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile("""{"type": "string", "pattern": "^[0-9]+(?:.[0-9]+)?$"}""", new JsonSchemaEvaluatorOptions { RegexProvider = (index, _) => { asked.Add(index); return null; } });
        CollectionAssert.AreEqual(new[] { -1 }, asked);
    }

    [TestMethod]
    public void RegisteredEntryPointsAreRecordedInTheImage()
    {
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(Schema);
        compiled.RegisterEntryPoints(["#/$defs/item", "#/$defs/left", "#/$defs/item"]);
        byte[] image = compiled.ToProgramImage();

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image);
        using JsonSchemaEvaluator item = loaded.ForEntryPoint("#/$defs/item");
        using JsonSchemaEvaluator left = loaded.ForEntryPoint("#/$defs/left");
        Assert.IsTrue(item.Evaluate("""{"id": 1}"""));
        Assert.IsTrue(left.Evaluate("""{"type": "left"}"""));
        Assert.IsFalse(left.Evaluate("""{"type": "right"}"""));
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => loaded.ForEntryPoint("#/$defs/right"));
    }
}
