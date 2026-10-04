// <copyright file="RuntimeCodeGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Runtime codegen: the generated entry gives the interpreter's results, and the experiment switch compiles schemas
/// when it is on.
/// </summary>
[TestClass]
public class RuntimeCodeGenerationTests
{
    private const string Schema = """
        {
          "type": "object",
          "required": ["id"],
          "properties": {"id": {"type": "integer", "minimum": 1}, "tags": {"type": "array", "items": {"type": "string"}}},
          "additionalProperties": false
        }
        """;

    private static readonly string[] Instances =
    [
        """{"id": 1}""", """{"id": 0}""", """{"id": 1, "tags": ["a"]}""", """{"id": 1, "tags": [1]}""",
        """{"tags": []}""", """{"id": 1, "x": 1}""", "[]", "1", "null",
    ];

    // Names of every length the dispatch treats differently: none, one masked word (1 to 8 bytes), several words with
    // the last overlapping (9 to 128, here with names that differ only in a first, middle or last word) and the
    // interpreter's lookup (longer); names of one length share a comparison chain.
    private const string NameLengths = """
        {
          "type": "object",
          "required": ["a", "abcdefghi"],
          "properties": {
            "": {"type": "null"},
            "a": {"type": "string"},
            "b": {"type": "boolean"},
            "ab": {"type": "integer"},
            "abc": {"type": "number"},
            "abcd": {"type": "array"},
            "abcde": {"type": "object"},
            "abcdef": {"type": ["string", "null"]},
            "abcdefg": {"type": "string"},
            "abcdefgh": {"type": "string"},
            "abcdefgx": {"type": "boolean"},
            "abcdefghi": {"type": "string"},
            "abcdefghj": {"type": "boolean"},
            "abcdefghijkl": {"type": "string"},
            "abcdefghijklmnop": {"type": "string"},
            "abcdefghijklmnopq": {"type": "string"},
            "abcdefghijklmnopqrstuvwx": {"type": "integer"},
            "abcdefghijklmnopqrstuvwy": {"type": "string"},
            "abcdefghijklmnopqrstuvwxyz0123": {"type": "boolean"},
            "abcdefghijklXnopqrstuvwxyz0123": {"type": "string"},
            "Xbcdefghijklmnopqrstuvwxyz0123": {"type": "null"},
            "nabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghz": {"type": "boolean"}
          },
          "additionalProperties": {"type": "string"}
        }
        """;

    // A recursive strict object whose properties are each kind of in-place leaf, and a child that is not specialised.
    private const string Leaves = """
        {
          "$ref": "#/$defs/node",
          "$defs": {
            "node": {
              "type": "object",
              "required": ["name"],
              "properties": {
                "name": {"type": "string"},
                "child": {"$ref": "#/$defs/node"},
                "kind": {"enum": ["a", "b"]},
                "tag": {"const": "x"},
                "code": {"type": "string", "minLength": 2, "maxLength": 4},
                "count": {"type": "integer"},
                "items": {"type": "array", "items": {"type": "string"}},
                "either": {"type": ["object", "string"], "properties": {"name": {"type": "boolean"}}, "additionalProperties": false}
              },
              "additionalProperties": false
            }
          }
        }
        """;

    private const string Maps = """
        {
          "type": "object",
          "minProperties": 1,
          "maxProperties": 3,
          "additionalProperties": {"type": "object", "additionalProperties": {"type": "integer"}}
        }
        """;

    // Arrays: of objects, of typed and leaf items, with bounds, with a type beside array, without items to test, and
    // (left to the interpreter) with prefix items and unique items.
    private const string Arrays = """
        {
          "type": "array",
          "maxItems": 3,
          "items": {
            "type": "object",
            "properties": {
              "a": {"type": "array", "items": {"type": "string"}},
              "b": {"type": "array", "items": {"type": "integer"}, "minItems": 1},
              "c": {"type": "array", "items": {"enum": ["a", "b"]}},
              "ab": {"type": ["array", "string"], "items": {"type": "array", "items": {"type": ["string", "null"]}}},
              "abc": {"type": "array"},
              "abcd": {"type": "array", "items": true, "maxItems": 1},
              "abcde": {"items": {"type": "object", "required": ["a"], "properties": {"a": {"type": "string"}}}},
              "name": {"type": "array", "prefixItems": [{"type": "string"}], "items": {"type": "integer"}},
              "tags": {"type": "array", "items": {"type": "string"}, "uniqueItems": true}
            }
          }
        }
        """;

    private const string Closed = """{"type": "object", "additionalProperties": false}""";

    private const string Untyped = """{"properties": {"a": {"type": "integer"}, "name": {"type": "string"}}}""";

    private const string Draft4 = """
        {
          "$schema": "http://json-schema.org/draft-04/schema#",
          "type": "object",
          "properties": {"count": {"type": "integer"}, "a": {"type": ["integer", "string"]}},
          "additionalProperties": {"type": "integer"}
        }
        """;

    private static readonly string[] Names =
    [
        string.Empty, "a", "b", "c", "ab", "abc", "abcd", "abcde", "abcdef", "abcdefg", "abcdefgh", "abcdefgx", "abcdefgy", "abcdefghi",
        "abcdefghj", "abcdefghk", "xbcdefghi", "abcdefghijkl", "abcdefghijklmnop", "abcdefghijklmnopq", "abcdefghijklmnopx",
        "abcdefghijklmnopqrstuvwxyz0123", "abcdefghijklmnopqrstuvwxyz0124", "abcdefghijklmnopqrstuvwx", "abcdefghijklmnopqrstuvwy",
        "abcdefghijklmnopXrstuvwx", "abcdefghijklXnopqrstuvwxyz0123", "abcdefghijklYnopqrstuvwxyz0123", "Xbcdefghijklmnopqrstuvwxyz0123",
        "nabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghz", "nabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghabcdefghy", "name", "child", "kind", "tag", "code", "count",
        "items", "either", "id", "tags", "other",
    ];

    private static readonly string[] Scalars =
    [
        "\"a\"", "\"b\"", "\"x\"", "\"c\"", "\"\"", "\"ab\"", "\"abcd\"", "\"abcde\"", "\"\\u0061\"", "\"\\u0078\"", "\"é\\u00e9\"",
        "0", "1", "-3", "1.0", "1.5", "1e2", "true", "false", "null", "[]", "[\"a\"]", "[1]", "{}",
    ];

    [TestMethod]
    [DataRow(Schema, 2)]
    [DataRow(NameLengths, 1)]
    [DataRow(Leaves, 3)]
    [DataRow(Maps, 2)]
    [DataRow(Arrays, 10)]
    [DataRow(Closed, 1)]
    [DataRow(Untyped, 1)]
    [DataRow(Draft4, 1)]
    public void GeneratedCodeGivesTheInterpretersResults(string schema, int specialisedNodes)
    {
        if (!SchemaLowering.IsSupported)
        {
            Assert.Inconclusive("Dynamic code is not supported here.");
        }

        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        CompiledSchema program = evaluator.Program;
        SchemaNode[] nodes = program.Nodes;
        SchemaNode entry = nodes[nodes[evaluator.RootNode].FlagEntry];
        NodeValidator compiled = SchemaLowering.Compile(nodes, entry, out int specialised);
        Assert.AreEqual(specialisedNodes, specialised, "The number of nodes given specialised methods.");

        var random = new Random(20261004);
        var builder = new System.Text.StringBuilder();
        int valid = 0;
        const int Count = 4000;
        for (int i = 0; i < Count + Instances.Length; i++)
        {
            string instance;
            if (i < Instances.Length)
            {
                instance = Instances[i];
            }
            else
            {
                builder.Clear();
                WriteValue(builder, random, 0);
                instance = builder.ToString();
            }

            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            IJsonDocument document = ((IJsonElement<JsonElement>)doc.RootElement).ParentDocument;
            int index = ((IJsonElement<JsonElement>)doc.RootElement).ParentDocumentIndex;
            bool expected = evaluator.Evaluate(doc.RootElement);
            bool actual = Evaluator.EvaluateFlagCompiled(compiled, program, nodes, entry.ResourceId, program.Options.MaxDepth, (Corvus.Text.Json.Internal.JsonDocument)document, document, evaluator.RootNode, index);
            Assert.AreEqual(expected, actual, instance);
            valid += expected ? 1 : 0;
        }

        // The documents exercise both results.
        Assert.IsTrue(valid > 0 && valid < Count, $"{valid} of {Count} documents are valid.");
    }

    // A random document over the schemas' names and values: objects and arrays at the root and (less often) beneath
    // it, with some names escaped, so that both the word comparisons and the slow lookup run.
    private static void WriteValue(System.Text.StringBuilder builder, Random random, int depth)
    {
        if (depth > 0 && (depth > 3 || random.Next(4) != 0))
        {
            builder.Append(Scalars[random.Next(Scalars.Length)]);
            return;
        }

        if (depth == 0 && random.Next(20) == 0)
        {
            builder.Append(Scalars[random.Next(Scalars.Length)]);
            return;
        }

        if (random.Next(depth == 0 ? 3 : 2) == 0)
        {
            builder.Append('[');
            int length = random.Next(5);
            for (int i = 0; i < length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                WriteValue(builder, random, depth + 1);
            }

            builder.Append(']');
            return;
        }

        builder.Append('{');
        int count = random.Next(5);

        // Some root objects start with the names one of the schemas requires, so that it accepts some documents.
        bool required = depth == 0 && random.Next(3) == 0;
        if (required)
        {
            builder.Append("\"a\":\"x\",\"abcdefghi\":\"x\"");
        }

        for (int i = 0; i < count; i++)
        {
            if (i > 0 || required)
            {
                builder.Append(random.Next(2) == 0 ? "," : " , ");
            }

            string name = Names[random.Next(Names.Length)];
            builder.Append('"');
            if (name.Length > 0 && random.Next(8) == 0)
            {
                builder.Append("\\u").Append(((int)name[0]).ToString("x4")).Append(name, 1, name.Length - 1);
            }
            else
            {
                builder.Append(name);
            }

            builder.Append("\":");
            WriteValue(builder, random, depth + 1);
        }

        builder.Append('}');
    }

    [TestMethod]
    public void TheSwitchCompilesSchemasWhenOn()
    {
        if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN") != "1" || Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_THRESHOLD") != "0" || !SchemaLowering.IsSupported)
        {
            Assert.Inconclusive("Runs only with CORVUS_RT_CODEGEN=1 and CORVUS_RT_CODEGEN_THRESHOLD=0.");
        }

        int before = SchemaLowering.CompiledCount;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"id": 2}""");
        Assert.IsTrue(evaluator.Evaluate(doc.RootElement));
        Assert.IsTrue(SchemaLowering.CompiledCount > before);
    }
}
#endif