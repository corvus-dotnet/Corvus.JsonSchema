// <copyright file="Coverage_GeneratorInlinerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the criterion inliners' fall-back paths. Each inliner inlines a statically
/// resolvable criterion but bails to a runtime <c>CompiledCriterion</c> (and the
/// <c>WorkflowExecutionContext</c>) for dynamic patterns or operand sources it cannot resolve at
/// generation time — those bail branches are exercised here.
/// </summary>
[TestClass]
public class Coverage_GeneratorInlinerTests
{
    [TestMethod]
    public void Dynamic_regex_pattern_falls_back_to_compiled_criterion()
    {
        // An embedded {expression} makes the regex pattern dynamic; it cannot be a [GeneratedRegex].
        string source = EmitWithSuccessCriteria(
            """{ "context": "$response.body#/name", "type": "regex", "condition": "^p{$inputs.petId}" }""");

        source.ShouldContain("CompiledCriterion");
        source.ShouldContain("WorkflowExecutionContext");
    }

    [TestMethod]
    public void Dynamic_jsonpath_query_falls_back_to_compiled_criterion()
    {
        string source = EmitWithSuccessCriteria(
            """{ "context": "$response.body", "type": "jsonpath", "condition": "$.tags[?(@=={$inputs.petId})]" }""");

        source.ShouldContain("CompiledCriterion");
    }

    [TestMethod]
    public void Simple_criterion_on_unsupported_source_falls_back_to_compiled_criterion()
    {
        // $url is not statically resolvable by the simple inliner, so the whole criterion is compiled.
        string source = EmitWithSuccessCriteria(
            """{ "condition": "$url == 'https://api/pets/1'" }""");

        source.ShouldContain("CompiledCriterion");
    }

    [TestMethod]
    public void Regex_on_unsupported_context_falls_back_to_compiled_criterion()
    {
        string source = EmitWithSuccessCriteria(
            """{ "context": "$url", "type": "regex", "condition": "^https" }""");

        source.ShouldContain("CompiledCriterion");
    }

    private static string EmitWithSuccessCriteria(string criterionJson)
    {
        string document = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
              "workflows": [
                {
                  "workflowId": "adopt",
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                      "successCriteria": [ {{criterionJson}} ],
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
            new WorkflowExecutorOptions("Acme.Pets.Workflows", "AdoptWorkflow", "Acme.Pets.AdoptInputs", "Acme.Pets.AdoptOutputs"));
    }
}
