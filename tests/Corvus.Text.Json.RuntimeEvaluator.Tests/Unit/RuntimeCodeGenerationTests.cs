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
                "id": {"const": 1},
                "other": {"type": "integer", "const": 100},
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

    // Entries the lowering does not specialise, with specialised objects and arrays beneath them: the interpreter
    // calls their generated methods.
    private const string UnderPatterns = """
        {
          "type": "object",
          "patternProperties": {
            "^x": {"type": "object", "properties": {"a": {"type": "string"}, "name": {"type": "integer"}}, "additionalProperties": false}
          },
          "additionalProperties": {"type": "array", "items": {"type": "object", "required": ["a"], "properties": {"a": {"type": "string"}}}}
        }
        """;

    private const string UnderAnyOf = """
        {
          "anyOf": [
            {"type": "string"},
            {
              "type": "object",
              "required": ["a"],
              "properties": {
                "a": {"type": "string"},
                "child": {"oneOf": [{"type": "null"}, {"type": "object", "properties": {"b": {"type": "boolean"}}, "additionalProperties": false}]}
              }
            }
          ]
        }
        """;

    private const string UnderIf = """
        {
          "type": "object",
          "properties": {
            "kind": {"type": "string"},
            "items": {"type": "array", "items": {"type": "object", "properties": {"a": {"type": "string"}}, "additionalProperties": false}}
          },
          "if": {"properties": {"kind": {"const": "a"}}},
          "then": {"required": ["a"]}
        }
        """;

    // An allOf/$ref chain of object schemas (a flat fused object), with a type union and a type dispatch beneath it.
    private const string Fused = """
        {
          "allOf": [
            {"$ref": "#/$defs/base"},
            {"properties": {"kind": {"enum": ["a", "b"]}, "count": {"type": "integer"}, "tags": {"type": "array", "items": {"type": "string"}}}}
          ],
          "$defs": {
            "base": {
              "type": "object",
              "required": ["a"],
              "properties": {
                "a": {"type": "string"},
                "name": {"anyOf": [{"type": "string"}, {"type": "number"}]},
                "child": {"oneOf": [{"type": "string", "minLength": 2}, {"type": "object", "properties": {"b": {"type": "boolean"}}, "additionalProperties": false}, {"type": "array", "items": {"type": "integer"}}]}
              }
            }
          }
        }
        """;

    // The object plan's general form: properties, several patterns, an additional schema and a dependency.
    private const string ObjectForms = """
        {
          "type": "object",
          "minProperties": 1,
          "properties": {"a": {"type": "string"}, "name": {"type": "string"}, "abcdefghi": {}},
          "patternProperties": {"^x": {"type": "integer"}, "e$": {"type": ["string", "integer"]}},
          "additionalProperties": {"type": ["boolean", "null", "string"]},
          "dependentRequired": {"a": ["abcdefghi"]}
        }
        """;

    // In-place applicators over a node's own keywords: a reference, allOf, anyOf and oneOf narrowed by type, and not.
    private const string Composition = """
        {
          "type": ["object", "string", "array"],
          "properties": {"a": {"type": "string"}},
          "required": ["a"],
          "allOf": [{"$ref": "#/$defs/notNull"}, {"not": {"const": "abcd"}}],
          "anyOf": [{"type": "string", "minLength": 2}, {"type": "object", "minProperties": 2}, {"type": "array", "maxItems": 1}, {"type": "object", "maxProperties": 3}],
          "oneOf": [{"type": "string", "maxLength": 3}, {"type": ["object", "array"]}, {"type": "string", "pattern": "^ab"}],
          "not": {"enum": ["x", "c"]},
          "$defs": {"notNull": {"not": {"type": "null"}}}
        }
        """;

    // A condition with both branches, and branches narrowed by the value's type.
    private const string Conditions = """
        {
          "if": {"properties": {"kind": {"const": "a"}}, "required": ["kind"]},
          "then": {"required": ["a"]},
          "else": {
            "anyOf": [
              {"type": "string", "minLength": 1},
              {"type": "array", "maxItems": 2},
              {"type": "object", "properties": {"count": {"type": "integer"}}},
              {"type": "object", "required": ["name"]}
            ]
          }
        }
        """;

    // The full fused pass: a condition decided by a value test, with patterns and an additional schema under it
    // (unknown names resolved after the pass), an alternative group of object branches, a required-only oneOf and a
    // forbidden set of names. The random documents repeat names, which hands an object to the interpreter.
    private const string FusedConditions = """
        {
          "type": "object",
          "properties": {"kind": {"enum": ["a", "b"]}, "a": {"type": "string"}},
          "allOf": [
            {
              "if": {"properties": {"kind": {"const": "a"}}, "required": ["kind"]},
              "then": {
                "required": ["a"],
                "patternProperties": {"^x": {"type": "integer"}},
                "additionalProperties": {"type": ["string", "integer", "boolean", "array"]}
              },
              "else": {"properties": {"count": {"type": "integer"}}}
            },
            {"anyOf": [{"properties": {"name": {"type": "string"}}, "required": ["name"]}, {"properties": {"tag": {"const": "x"}}, "maxProperties": 4}]},
            {"oneOf": [{"required": ["abcdefghi"]}, {"required": ["b"]}]},
            {"not": {"required": ["c", "ab"]}}
          ]
        }
        """;

    // A oneOf whose branches a property's value selects.
    private const string Discriminated = """
        {
          "oneOf": [
            {"type": "object", "properties": {"kind": {"const": "a"}, "count": {"type": "integer"}}, "required": ["kind"]},
            {"type": "object", "properties": {"kind": {"const": "b"}, "name": {"type": "string"}}, "required": ["kind"]},
            {"type": "object", "properties": {"kind": {"enum": ["c", "x"]}}, "required": ["kind", "a"]}
          ]
        }
        """;

    // unevaluatedProperties over branches that apply always and under a condition, with a pattern under the condition.
    private const string Unevaluated = """
        {
          "type": "object",
          "properties": {"a": {"type": "string"}},
          "allOf": [
            {"properties": {"name": {"type": "string"}}},
            {
              "if": {"properties": {"kind": {"const": "a"}}, "required": ["kind"]},
              "then": {"properties": {"count": {"type": "integer"}}, "patternProperties": {"^x": {}}}
            }
          ],
          "unevaluatedProperties": {"type": ["string", "boolean"]}
        }
        """;

    // Many names of one length (told apart by a window of their bits, then the whole word), and leaves of each kind
    // compiled in place: integer bounds as longs, a bound that is not an integer, a set of strings by words, a
    // number const, a mixed enum, a pattern and a length.
    private const string NamesAndLeaves = """
        {
          "type": "object",
          "properties": {
            "kind1": {"type": "integer", "minimum": 0, "maximum": 1},
            "kind2": {"type": "integer", "exclusiveMinimum": -3, "exclusiveMaximum": 100, "multipleOf": 2},
            "kind3": {"type": "number", "minimum": 0.5},
            "kind4": {"enum": ["a", "b", "x", "c", "ab", "abcd", ""]},
            "kind5": {"const": 1},
            "kind6": {"enum": ["a", 1, null, true, [1]]},
            "kindA": {"type": "string", "pattern": "^a", "minLength": 1},
            "xind1": {"minimum": 1},
            "kinda": {"type": ["string", "integer"], "maxLength": 2, "maximum": 0},
            "kindb": {"const": "abcde"}
          },
          "additionalProperties": {"type": "array", "items": {"minimum": 1, "maxLength": 1}}
        }
        """;

    private const string Closed = """{"type": "object", "additionalProperties": false}""";

    private const string Untyped = """{"properties": {"a": {"type": "integer"}, "name": {"type": "string"}}}""";

    // Nodes the interpreter has no plan for: propertyNames (a pattern, a length, and an enum, which is evaluated as a
    // document), and keywords that cannot apply under the node's type.
    private const string WithoutPlans = """
        {
          "type": "object",
          "properties": {
            "name": {"type": "object", "propertyNames": {"pattern": "^[a-k]+$"}, "additionalProperties": {"type": "string"}},
            "child": {"propertyNames": {"maxLength": 4}},
            "kind": {"type": "object", "propertyNames": {"enum": ["a", "b", "kind"]}, "additionalProperties": true},
            "tag": {"type": "string", "uniqueItems": true, "minLength": 1},
            "code": {"type": "string", "additionalProperties": false},
            "items": {"type": "array", "minLength": 3, "items": {"type": "string"}, "maxItems": 3},
            "tags": {"type": "object", "items": {"type": "string"}},
            "either": {"type": "array", "items": {"type": ["string", "integer"]}, "contains": {"type": "integer"}},
            "other": {"type": "array", "contains": {"const": "a"}, "minContains": 2, "maxContains": 3},
            "id": {"contains": {"type": "string", "minLength": 2}, "minContains": 0, "maxContains": 1}
          }
        }
        """;

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
        "items", "either", "id", "tags", "other", "kind1", "kind2", "kind3", "kind4", "kind5", "kind6", "kindA", "xind1", "kinda", "kindb", "kindc",
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
    [DataRow(UnderPatterns, 4)]
    [DataRow(ObjectForms, 1)]
    [DataRow(Composition, 6)]
    [DataRow(Conditions, 7)]
    [DataRow(UnderAnyOf, 4)]
    [DataRow(Fused, 8)]
    [DataRow(UnderIf, 5)]
    [DataRow(FusedConditions, 13)]
    [DataRow(NamesAndLeaves, 1)]
    [DataRow(Discriminated, 4)]
    [DataRow(Unevaluated, 5)]
    [DataRow(Closed, 1)]
    [DataRow(Untyped, 1)]
    [DataRow(Draft4, 1)]
    [DataRow(WithoutPlans, 11)]
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
        NodeValidator? compiled = SchemaLowering.Compile(nodes, entry, out SchemaNode[] generatedNodes, out int specialised);
        // At least as many as when the schema was added: a later increment specialises more, and none should specialise less.
        Assert.IsTrue(specialised >= specialisedNodes, $"{specialised} nodes given specialised methods, at least {specialisedNodes} expected (entry plan {entry.Plan}, entry {(compiled is null ? "interpreted" : "generated")}).");

        // Count the interpreter's calls of generated methods (generated methods call each other directly).
        int callsFromInterpreter = 0;
        foreach (SchemaNode node in generatedNodes)
        {
            if (node?.Plan == NodePlan.Generated)
            {
                NodeValidator method = node.Generated!;
                node.Generated = (ref EvaluationState state, IJsonDocument doc, int at) =>
                {
                    callsFromInterpreter++;
                    return method(ref state, doc, at);
                };
            }
        }

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
            var parsed = (Corvus.Text.Json.Internal.JsonDocument)document;

            // As the evaluator runs a compiled schema: the entry's method, or the interpreter over the generated nodes.
            bool actual = compiled is not null
                ? Evaluator.EvaluateFlagCompiled(compiled, program, generatedNodes, entry.ResourceId, program.Options.MaxDepth, parsed, document, evaluator.RootNode, index)
                : Evaluator.EvaluateFlagRaw(program, generatedNodes, generatedNodes[entry.Id], entry.ResourceId, program.Options.MaxDepth, parsed, document, evaluator.RootNode, index);
            Assert.AreEqual(expected, actual, instance);
            valid += expected ? 1 : 0;
        }

        // A schema whose entry is not specialised reaches its generated methods from the interpreter.
        if (compiled is null && (schema == UnderIf || schema == UnderAnyOf))
        {
            Assert.IsTrue(callsFromInterpreter > 0, "The interpreter called no generated method.");
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
    public void FlagModeEvaluationAllocatesNothing()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"id": 2, "tags": ["a"]}""");
        ParsedJsonDocument<JsonElement>[] docs = [doc, doc, doc, doc];

        // Warm up past tiering, so that the measured calls run the optimised code.
        for (int i = 0; i < 20_000; i++)
        {
            EvaluateAll(evaluator, docs);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int valid = 0;
        for (int i = 0; i < 1000; i++)
        {
            valid += EvaluateAll(evaluator, docs);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(4000, valid);
        Assert.AreEqual(0L, allocated, "Bytes allocated by 4000 flag-mode evaluations.");

        // A small method of its own: the evaluator's entry is inlined here, as it is into an application's loop.
        static int EvaluateAll(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] docs)
        {
            int valid = 0;
            foreach (ParsedJsonDocument<JsonElement> d in docs)
            {
                valid += evaluator.Evaluate(d.RootElement) ? 1 : 0;
            }

            return valid;
        }
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