// <copyright file="WorkflowIdEscapingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// A <c>workflowId</c> is attacker-authored: it arrives inside a document the platform compiles and runs, and the
/// metaschema constrains it no further than <c>type: string</c>. These tests hold the emitter to the rule every other
/// emission site follows, which is that such a value reaches generated source only through
/// <see cref="EmitText"/>'s quoting.
/// </summary>
/// <remarks>
/// <para>
/// Both emissions are covered deliberately. The durable form emits the id twice with a newline between them inside a
/// ternary, so a breakout leaves the first branch complete and the second unparseable — the injection degrades to a
/// compile failure. That is a property of how the text happens to be laid out, not a control: it holds only while the
/// emission stays split across two lines, and nothing would catch a later refactor to a single line. The non-durable
/// form emits the id once in a single statement, where the same payload is a clean breakout.
/// </para>
/// <para>
/// Testing only the durable form would therefore assert the accident rather than the fix, and would keep passing if the
/// quoting were removed again.
/// </para>
/// </remarks>
[TestClass]
public class WorkflowIdEscapingTests
{
    // A quote closes the literal, the parenthesis and semicolon complete the call, and the line comment swallows
    // whatever the emitter appends after the injection point. The newline is what a single-line emission cannot absorb.
    private const string HostileWorkflowId = """adopt"); System.Console.WriteLine("pwned"); //""";

    private const string HostileWorkflowIdWithNewline = "adopt\"); Evil();\n//";

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_hostile_workflow_id_does_not_break_out_of_the_activity_name_literal(bool durable)
    {
        string source = Emit(HostileWorkflowId, durable);

        // Parsing is the assertion, because the property is syntactic and no substring test states it: the payload may
        // legitimately appear inside a comment or inside a correctly escaped literal, and it is only a defect when it
        // ends one of those and starts being parsed as code. Earlier versions of this test asserted the payload was
        // absent from the file, which flagged both of the safe placements.
        ShouldParseCleanly(source);

        // ...and the id does reach the activity name, with its quote escaped, rather than being dropped on the floor.
        source.ShouldContain("""adopt\"); System.Console.WriteLine""");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_workflow_id_that_closes_and_reopens_a_literal_still_parses(bool durable)
    {
        // The payload that survives a naive "does the line still balance its quotes" check: it closes the literal,
        // injects a call, and opens a fresh literal so the count comes out even again.
        string source = Emit("""adopt"); Evil(); x = ("stillOpen""", durable);

        ShouldParseCleanly(source);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_workflow_id_containing_a_newline_stays_on_one_line(bool durable)
    {
        string source = Emit(HostileWorkflowIdWithNewline, durable);

        // A raw newline inside a string literal does not compile, so the escaped form is what keeps the emission valid.
        source.ShouldContain("""adopt\"); Evil();\n//""");

        // Every line that starts an activity must still terminate its own literal: an unbalanced count means the
        // remainder of the emitted file is being parsed as string content.
        foreach (string line in source.Split('\n'))
        {
            if (line.Contains("StartActivity(", StringComparison.Ordinal))
            {
                UnescapedQuoteCount(line).ShouldBeGreaterThan(0);
                (UnescapedQuoteCount(line) % 2).ShouldBe(0, $"unbalanced literal on: {line.Trim()}");
            }
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_workflow_id_cannot_break_out_of_a_generated_doc_comment(bool durable)
    {
        // The id is also written into `///` summaries. A line break there ends the comment and everything after it is
        // compiled as code, which is the same breakout as an unterminated literal one context over. The audit named the
        // three literal sites; these comment sites were found by the test written for them.
        string source = Emit("adopt\nclass Injected { static Injected() { Evil(); } } //", durable);

        // A newline ends the `///` comment, so the declaration after it becomes a real type at namespace scope. That
        // is a parse-clean injection, not a syntax error, so this one needs the structural assertion as well.
        ShouldParseCleanly(source);
        foreach (string line in source.Split('\n'))
        {
            line.TrimStart().ShouldNotStartWith(
                "class Injected",
                customMessage: "the workflow id ended the doc comment and became a declaration.");
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_workflow_id_cannot_make_a_doc_comment_badly_formed_xml(bool durable)
    {
        // Not a breakout, but every consuming project that documents its public API compiles generated code with XML
        // documentation on, and a bare angle bracket there is a diagnostic rather than a curiosity.
        string source = Emit("adopt<not-a-tag>", durable);

        // Scoped to the doc comments on purpose: an angle bracket inside a C# string literal is ordinary text, and
        // asserting against the whole file would forbid the activity name carrying the id at all.
        foreach (string line in source.Split('\n').Where(l => l.TrimStart().StartsWith("///", StringComparison.Ordinal)))
        {
            line.ShouldNotContain(
                "adopt<not-a-tag>",
                customMessage: $"a doc comment carries unescaped XML metacharacters: {line.Trim()}");
        }

        source.ShouldContain("adopt&lt;not-a-tag&gt;");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "non-durable")]
    [DataRow(true, DisplayName = "durable")]
    public void A_benign_workflow_id_is_still_the_activity_name(bool durable)
    {
        // The escaping must not change what a well-formed document generates.
        string source = Emit("adoptPet", durable);

        source.ShouldContain("\"workflow.adoptPet\"");
    }

    // The emitted source must be syntactically valid C#. Every breakout this file tests — ending a string literal,
    // ending a doc comment with a newline — shows up here as a parse error, and nothing that stays inside a literal or
    // a comment does. Semantic diagnostics are not checked: the fixture's types are not in scope, and they are not what
    // an injection breaks.
    private static void ShouldParseCleanly(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        string[] errors = [.. tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id} at {d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}")];

        errors.ShouldBeEmpty($"the generated executor does not parse:{System.Environment.NewLine}{string.Join(System.Environment.NewLine, errors)}");
    }

    // Counts quote characters that actually open or close a literal, skipping any preceded by a backslash.
    private static int UnescapedQuoteCount(string line)
    {
        int count = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                count++;
            }
        }

        return count;
    }

    private static string Emit(string workflowId, bool durable)
    {
        string escapedForJson = workflowId.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        string document = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Pets", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "petstore", "url": "https://example.test/pets.json", "type": "openapi" }
              ],
              "workflows": [
                {
                  "workflowId": "{{escapedForJson}}",
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "1" } ],
                      "outputs": { "petName": "$response.body#/name" }
                    }
                  ],
                  "outputs": { "name": "$steps.getPet.outputs.petName" }
                }
              ]
            }
            """;

        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                "Acme.Pets.GetPetRequest",
                "Acme.Pets.GetPetResponse",
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")],
                "Acme.Pets.PetsClient",
                "GetPetAsync",
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("Acme.Pets.Workflows", "AdoptWorkflow", "Acme.Pets.AdoptInputs", "Acme.Pets.AdoptOutputs", Durable: durable));
    }
}