// <copyright file="WorkflowDocumentAnalyzerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves <see cref="WorkflowDocumentAnalyzer"/> collects EVERY semantic finding with a JSON Pointer
/// (the generator fails fast on the first; the designer's Problems tray needs them all): goto/retry
/// targets, dependsOn references, criterion syntax through the runtime's own compiler, runtime
/// expressions, reusable component references, duplicate ids, and conservative reachability. It
/// never throws on a malformed document (structure is the schema pass's job).
/// </summary>
[TestClass]
public class WorkflowDocumentAnalyzerTests
{
    [TestMethod]
    public void A_clean_document_yields_no_diagnostics()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Clean", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "pets", "url": "./pets.json", "type": "openapi" }],
          "workflows": [
            {
              "workflowId": "clean",
              "steps": [
                {
                  "stepId": "first",
                  "operationId": "listPets",
                  "successCriteria": [{ "condition": "$statusCode == 200" }],
                  "onFailure": [{ "name": "retry-it", "type": "retry", "stepId": "first", "retryAfter": 5, "retryLimit": 2 }],
                  "outputs": { "pets": "$response.body" }
                },
                { "stepId": "second", "operationId": "listPets" }
              ],
              "outputs": { "all": "$steps.first.outputs.pets" }
            }
          ]
        }
        """);

        diagnostics.ShouldBeEmpty();
    }

    [TestMethod]
    public void Every_finding_is_collected_not_just_the_first()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "arazzo": "1.1.0",
          "workflows": [
            {
              "workflowId": "broken",
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "onSuccess": [{ "name": "jump", "type": "goto", "stepId": "missing-step" }],
                  "onFailure": [{ "name": "jump2", "type": "goto", "workflowId": "missing-workflow" }]
                },
                {
                  "stepId": "b",
                  "operationId": "x",
                  "dependsOn": ["nope"],
                  "successCriteria": [{ "condition": "$statusCode ==" }]
                }
              ]
            }
          ]
        }
        """);

        // Four authored defects PLUS the knock-on: a's catch-all goto blocks the fall-through and its
        // target is unknown, so no edge reaches b — the reachability warning is genuinely true here.
        diagnostics.Count.ShouldBe(5);
        diagnostics.Select(d => d.Category).ShouldBe(["goto-target", "goto-target", "depends-on", "criterion-syntax", "reachability"], ignoreOrder: true);
        diagnostics.Count(d => d.Severity == WorkflowDocumentDiagnosticSeverity.Error).ShouldBe(4);
    }

    [TestMethod]
    public void Goto_and_retry_targets_are_pointered_to_the_action()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                { "stepId": "a", "operationId": "x", "onSuccess": [{ "name": "j", "type": "goto", "stepId": "ghost" }] }
              ]
            }
          ]
        }
        """);

        WorkflowDocumentDiagnostic d = diagnostics.ShouldHaveSingleItem();
        d.Category.ShouldBe("goto-target");
        d.InstancePath.ShouldBe("/workflows/0/steps/0/onSuccess/0");
        d.Message.ShouldContain("ghost");
    }

    [TestMethod]
    public void An_unknown_goto_workflow_softens_to_a_warning_when_arazzo_sources_exist()
    {
        const string Template = """
        {
          "sourceDescriptions": [{{sources}}],
          "workflows": [
            {
              "workflowId": "w",
              "steps": [{ "stepId": "a", "operationId": "x", "onSuccess": [{ "name": "j", "type": "goto", "workflowId": "elsewhere" }] }]
            }
          ]
        }
        """;

        Analyze(Template.Replace("{{sources}}", """{ "name": "lib", "url": "./lib.arazzo.json", "type": "arazzo" }"""))
            .ShouldHaveSingleItem().Severity.ShouldBe(WorkflowDocumentDiagnosticSeverity.Warning);
        Analyze(Template.Replace("{{sources}}", """{ "name": "pets", "url": "./pets.json", "type": "openapi" }"""))
            .ShouldHaveSingleItem().Severity.ShouldBe(WorkflowDocumentDiagnosticSeverity.Error);
    }

    [TestMethod]
    public void Unknown_dependsOn_is_an_error_even_though_the_generator_ignores_it()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                { "stepId": "a", "operationId": "x" },
                { "stepId": "b", "operationId": "x", "dependsOn": ["a", "ghost"] }
              ]
            }
          ]
        }
        """);

        WorkflowDocumentDiagnostic d = diagnostics.ShouldHaveSingleItem();
        d.Category.ShouldBe("depends-on");
        d.InstancePath.ShouldBe("/workflows/0/steps/1/dependsOn/1");
    }

    [TestMethod]
    public void Criterion_syntax_checks_with_the_runtimes_own_compiler()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "successCriteria": [
                    { "condition": "$statusCode == 200 &&" },
                    { "context": "$response.body", "type": "regex", "condition": "(unclosed" },
                    { "type": "jsonpath", "condition": "$[?count(@.pets) > 0]" }
                  ]
                }
              ]
            }
          ]
        }
        """);

        // The malformed simple condition and the broken regex report; jsonpath without a context is
        // the third finding (the runtime requires one). All are positioned on their criterion.
        diagnostics.Count.ShouldBe(3);
        diagnostics[0].Category.ShouldBe("criterion-syntax");
        diagnostics[0].InstancePath.ShouldBe("/workflows/0/steps/0/successCriteria/0/condition");
        diagnostics[1].InstancePath.ShouldStartWith("/workflows/0/steps/0/successCriteria/1");
        diagnostics[2].InstancePath.ShouldStartWith("/workflows/0/steps/0/successCriteria/2");
    }

    [TestMethod]
    public void Xpath_criteria_round_trip_with_a_not_evaluated_warning()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "successCriteria": [{ "context": "$response.body", "type": "xpath", "condition": "//pet" }]
                }
              ]
            }
          ]
        }
        """);

        WorkflowDocumentDiagnostic d = diagnostics.ShouldHaveSingleItem();
        d.Severity.ShouldBe(WorkflowDocumentDiagnosticSeverity.Warning);
        d.Category.ShouldBe("criterion-type");
    }

    [TestMethod]
    public void Malformed_runtime_expressions_report_in_outputs_parameters_and_request_bodies()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "outputs": { "bad": "$nonsense.thing" },
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "parameters": [{ "name": "petId", "in": "path", "value": "$inputs.petId" }, { "name": "bad", "in": "query", "value": "$bogus(" }],
                  "requestBody": { "payload": "$payload-of.x", "replacements": [{ "target": "/id", "value": "$inputs.id" }] },
                  "outputs": { "ok": "$response.body", "bad": "$respons.body" }
                }
              ]
            }
          ]
        }
        """);

        diagnostics.ShouldAllBe(d => d.Category == "expression-syntax");
        diagnostics.Select(d => d.InstancePath).ShouldBe(
            [
                "/workflows/0/outputs/bad",
                "/workflows/0/steps/0/parameters/1/value",
                "/workflows/0/steps/0/outputs/bad",
                "/workflows/0/steps/0/requestBody/payload",
            ],
            ignoreOrder: true);
    }

    [TestMethod]
    public void Dangling_reusable_references_report_and_resolved_ones_check_their_goto_target()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "components": {
            "failureActions": { "give-up": { "name": "give-up", "type": "end" }, "jump-away": { "name": "jump-away", "type": "goto", "stepId": "ghost" } },
            "parameters": { "page": { "name": "page", "in": "query", "value": 1 } }
          },
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "parameters": [{ "reference": "$components.parameters.page" }, { "reference": "$components.parameters.missing" }],
                  "onFailure": [
                    { "reference": "$components.failureActions.give-up" },
                    { "reference": "$components.failureActions.nope" },
                    { "reference": "$components.failureActions.jump-away" }
                  ]
                }
              ]
            }
          ]
        }
        """);

        diagnostics.Count.ShouldBe(3);
        diagnostics.Single(d => d.InstancePath == "/workflows/0/steps/0/parameters/1/reference").Category.ShouldBe("component-reference");
        diagnostics.Single(d => d.InstancePath == "/workflows/0/steps/0/onFailure/1/reference").Category.ShouldBe("component-reference");

        // The resolved reusable action's goto target participates in THIS workflow.
        diagnostics.Single(d => d.Category == "goto-target").InstancePath.ShouldBe("/workflows/0/steps/0/onFailure/2");
    }

    [TestMethod]
    public void Duplicate_workflow_and_step_ids_report()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            { "workflowId": "same", "steps": [{ "stepId": "a", "operationId": "x" }, { "stepId": "a", "operationId": "x" }] },
            { "workflowId": "same", "steps": [{ "stepId": "only", "operationId": "x" }] }
          ]
        }
        """);

        diagnostics.Count.ShouldBe(2);
        diagnostics.Select(d => d.InstancePath).ShouldBe(["/workflows/1/workflowId", "/workflows/0/steps/1/stepId"], ignoreOrder: true);
        diagnostics.ShouldAllBe(d => d.Category == "duplicate-id");
    }

    [TestMethod]
    public void A_step_behind_a_catch_all_divert_warns_as_unreachable()
    {
        // a's catch-all (criteria-less) goto ALWAYS fires (first-match-wins), so the default
        // "continue to b" can never happen and nothing else jumps to b.
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                { "stepId": "a", "operationId": "x", "onSuccess": [{ "name": "skip", "type": "goto", "stepId": "c" }] },
                { "stepId": "b", "operationId": "x" },
                { "stepId": "c", "operationId": "x" }
              ]
            }
          ]
        }
        """);

        WorkflowDocumentDiagnostic d = diagnostics.ShouldHaveSingleItem();
        d.Severity.ShouldBe(WorkflowDocumentDiagnosticSeverity.Warning);
        d.Category.ShouldBe("reachability");
        d.InstancePath.ShouldBe("/workflows/0/steps/1");
    }

    [TestMethod]
    public void A_conditional_goto_keeps_the_fall_through_so_nothing_is_unreachable()
    {
        IReadOnlyList<WorkflowDocumentDiagnostic> diagnostics = Analyze("""
        {
          "workflows": [
            {
              "workflowId": "w",
              "steps": [
                {
                  "stepId": "a",
                  "operationId": "x",
                  "onSuccess": [{ "name": "skip", "type": "goto", "stepId": "c", "criteria": [{ "condition": "$statusCode == 202" }] }]
                },
                { "stepId": "b", "operationId": "x" },
                { "stepId": "c", "operationId": "x" }
              ]
            }
          ]
        }
        """);

        diagnostics.ShouldBeEmpty();
    }

    [TestMethod]
    public void A_malformed_document_never_throws()
    {
        Analyze("""{ "workflows": "not-an-array" }""").ShouldBeEmpty();
        Analyze("""{ "workflows": [{ "workflowId": 42, "steps": [{ "stepId": {}, "onSuccess": [null, "text", { "type": "goto" }] }] }] }""").ShouldBeEmpty();
        Analyze("""[1, 2, 3]""").ShouldBeEmpty();
    }

    private static IReadOnlyList<WorkflowDocumentDiagnostic> Analyze(string json)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        return WorkflowDocumentAnalyzer.Analyze(document.RootElement);
    }
}