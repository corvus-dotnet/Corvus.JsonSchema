// <copyright file="MalformedStringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A string that is not well-formed text (an escape of half of a surrogate pair, or bytes that are not UTF-8) is
/// accepted when the document is parsed, and is checked when a keyword reads it: a keyword that reads its characters
/// reports it not valid, a keyword that compares it finds it equal to no well-formed text, and nothing throws
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1021).
/// </summary>
[TestClass]
public class MalformedStringTests
{
    private static readonly string[] ValueSchemas =
    [
        """{"pattern": "^.$"}""",
        """{"pattern": "^(.)\\1?$"}""",
        """{"pattern": "^a"}""",
        """{"minLength": 1}""",
        """{"maxLength": 5}""",
        """{"enum": ["a", "b"]}""",
        """{"const": "a"}""",
        """{"format": "email"}""",
        """{"format": "idn-email"}""",
        """{"format": "iri"}""",
        """{"format": "date"}""",
        """{"format": "uri"}""",
        """{"$schema": "http://json-schema.org/draft-07/schema#", "contentMediaType": "application/json"}""",
    ];

    private static readonly string[] NameSchemas =
    [
        """{"propertyNames": {"pattern": "^a"}}""",
        """{"propertyNames": {"maxLength": 3}}""",
        """{"propertyNames": {"format": "idn-hostname"}}""",
        """{"patternProperties": {"^a": {}}, "additionalProperties": false}""",
        """{"properties": {"a": {}}, "additionalProperties": false}""",
    ];

    // Each is the JSON text of a string, as bytes: some are escapes of half of a surrogate pair, some are bytes that
    // are not UTF-8, and some are both.
    private static readonly Dictionary<string, byte[]> Malformed = new()
    {
        ["lone high surrogate escape"] = Encoding.UTF8.GetBytes("\"\\ud800\""),
        ["lone high surrogate escape at the end"] = Encoding.UTF8.GetBytes("\"a\\ud83d\""),
        ["lone low surrogate escape"] = Encoding.UTF8.GetBytes("\"\\udc00\""),
        ["low surrogate escape after a character"] = Encoding.UTF8.GetBytes("\"a\\udc00\""),
        ["high surrogate escape before a character"] = Encoding.UTF8.GetBytes("\"\\ud800x\""),
        ["high surrogate escape before another escape"] = Encoding.UTF8.GetBytes("\"\\ud800\\u0061\""),
        ["two high surrogate escapes"] = Encoding.UTF8.GetBytes("\"\\ud800\\ud800\""),
        ["a surrogate encoded in UTF-8"] = [0x22, 0xED, 0xA0, 0x80, 0x22],
        ["a lead byte without its continuation"] = [0x22, 0xC3, 0x28, 0x22],
        ["a byte that is never UTF-8"] = [0x22, 0xFF, 0x22],
        ["a byte that is never UTF-8 after an escape"] = [0x22, 0x5C, 0x6E, 0xFF, 0x22],
    };

    // Well-formed text that is not all ASCII, escaped or not: every keyword reads it as it always has.
    private static readonly (string Name, byte[] Json, string Schema, bool Expected)[] WellFormed =
    [
        ("e acute", Encoding.UTF8.GetBytes("\"\u00e9\""), """{"pattern": "^.$"}""", true),
        ("e acute", Encoding.UTF8.GetBytes("\"\u00e9\""), """{"minLength": 1, "maxLength": 1}""", true),
        ("escaped e acute", Encoding.UTF8.GetBytes("\"\\u00e9\""), """{"pattern": "^.$", "maxLength": 1}""", true),
        ("escaped e acute", Encoding.UTF8.GetBytes("\"\\u00e9\""), """{"const": "\u00e9"}""", true),
        ("surrogate pair escape", Encoding.UTF8.GetBytes("\"\\ud83d\\ude00\""), """{"pattern": "^.$", "minLength": 1, "maxLength": 1}""", true),
        ("surrogate pair escape", Encoding.UTF8.GetBytes("\"\\ud83d\\ude00\""), """{"enum": ["\ud83d\ude00"]}""", true),
        ("emoji", Encoding.UTF8.GetBytes("\"\ud83d\ude00x\""), """{"pattern": "^.x$", "maxLength": 2}""", true),
        ("emoji", Encoding.UTF8.GetBytes("\"\ud83d\ude00x\""), """{"maxLength": 1}""", false),
        ("unicode email", Encoding.UTF8.GetBytes("\"\u00e9@example.com\""), """{"format": "idn-email"}""", true),
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (bool builder in new[] { false, true })
        {
            foreach (JsonSchemaCodeGeneration generation in new[] { JsonSchemaCodeGeneration.Disabled, JsonSchemaCodeGeneration.Eager })
            {
                foreach (string name in Malformed.Keys)
                {
                    foreach (string schema in ValueSchemas)
                    {
                        yield return [name, schema, false, generation, builder];
                    }

                    foreach (string schema in NameSchemas)
                    {
                        yield return ["key " + name, schema, false, generation, builder];
                    }

                    // A keyword that does not read the characters.
                    yield return [name, """{"type": "string"}""", true, generation, builder];
                    yield return ["key " + name, """{"properties": {"a": {}}}""", true, generation, builder];
                    yield return ["key " + name, """{"minProperties": 1}""", true, generation, builder];
                }

                foreach ((string name, _, string schema, bool expected) in WellFormed)
                {
                    yield return ["well-formed " + name, schema, expected, generation, builder];
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void AKeywordThatReadsAMalformedStringReportsItNotValid(string name, string schema, bool expected, JsonSchemaCodeGeneration generation, bool builder)
    {
        byte[] instance = name.StartsWith("key ", StringComparison.Ordinal)
            ? [.. "{"u8.ToArray(), .. Malformed[name.Substring(4)], .. ": 1}"u8.ToArray()]
            : name.StartsWith("well-formed ", StringComparison.Ordinal)
                ? WellFormed.First(w => w.Name == name.Substring(12)).Json
                : Malformed[name];

        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true, CodeGeneration = generation };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, options);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);

        if (builder)
        {
            // A document that is not parsed text is read through its interface.
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using JsonDocumentBuilder<JsonElement.Mutable> built = doc.RootElement.CreateBuilder(workspace);
            AssertEvaluates(evaluator, built.RootElement, expected);
        }
        else
        {
            AssertEvaluates(evaluator, doc.RootElement, expected);
        }
    }

    [TestMethod]
    public void TwoDifferentLoneSurrogateEscapesAreDifferentItems()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"uniqueItems": true}""");
        Assert.IsTrue(evaluator.Evaluate("""["\ud800", "\udc00", "\ud800x"]"""));
        Assert.IsFalse(evaluator.Evaluate("""["\ud800", "\ud800"]"""));

        // An escape and the bytes it is read as are one item.
        Assert.IsFalse(evaluator.Evaluate(new ReadOnlyMemory<byte>([.. "[\"\\ud800\", \""u8.ToArray(), 0xED, 0xA0, 0x80, .. "\"]"u8.ToArray()])));
    }

    private static void AssertEvaluates<T>(JsonSchemaEvaluator evaluator, in T instance, bool expected)
        where T : struct, IJsonElement<T>
    {
        Assert.AreEqual(expected, evaluator.Evaluate(instance), "flag mode");
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.AreEqual(expected, evaluator.Evaluate(instance, collector), "with a results collector");
    }
}