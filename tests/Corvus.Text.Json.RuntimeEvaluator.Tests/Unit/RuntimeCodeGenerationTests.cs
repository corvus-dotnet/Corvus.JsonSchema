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

    [TestMethod]
    public void GeneratedEntryGivesTheInterpretersResults()
    {
        if (!SchemaLowering.IsSupported)
        {
            Assert.Inconclusive("Dynamic code is not supported here.");
        }

        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema);
        CompiledSchema program = evaluator.Program;
        SchemaNode[] nodes = program.Nodes;
        SchemaNode entry = nodes[nodes[evaluator.RootNode].FlagEntry];
        NodeValidator compiled = SchemaLowering.Compile(nodes, entry);
        foreach (string instance in Instances)
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            IJsonDocument document = ((IJsonElement<JsonElement>)doc.RootElement).ParentDocument;
            int index = ((IJsonElement<JsonElement>)doc.RootElement).ParentDocumentIndex;
            bool expected = evaluator.Evaluate(doc.RootElement);
            bool actual = Evaluator.EvaluateFlagCompiled(compiled, program, nodes, entry.ResourceId, program.Options.MaxDepth, (Corvus.Text.Json.Internal.JsonDocument)document, document, evaluator.RootNode, index);
            Assert.AreEqual(expected, actual, instance);
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