// <copyright file="Coverage_GeneratorEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the executor emitter's guard clauses and reusable-action resolution edges:
/// control flow on a channel step, an unbindable step, the sub-workflow criteria restriction, and
/// malformed/edge reusable-action references. These not-yet-supported or error boundaries are not hit by
/// the happy-path emitter tests.
/// </summary>
[TestClass]
public class Coverage_GeneratorEmitterTests
{
    [TestMethod]
    public void Channel_step_with_success_criteria_is_not_supported()
    {
        var descriptor = new AsyncApiChannelDescriptor(
            "ch",
            OperationAction.Send,
            "place",
            "Gen.Events.PlaceProducer",
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, "PublishOrderAsync")]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        Should.Throw<NotSupportedException>(() => Emit(binder, """
            {
              "stepId": "s", "channelPath": "ch", "action": "send",
              "requestBody": { "payload": "$inputs.m" },
              "successCriteria": [ { "condition": "$statusCode == 200" } ]
            }
            """));
    }

    [TestMethod]
    public void Step_with_no_resolvable_target_throws()
    {
        // A step with none of operationId/operationPath/workflowId/channelPath binds to None and is
        // rejected by the emitter.
        Should.Throw<InvalidOperationException>(() => Emit(new WorkflowOperationBinder([]), """{ "stepId": "s" }"""));
    }

    [TestMethod]
    public void SubWorkflow_step_criterion_referencing_response_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => Emit(new WorkflowOperationBinder([]), """
            {
              "stepId": "s", "workflowId": "child",
              "successCriteria": [ { "condition": "$response.body#/x == 1" } ]
            }
            """));
    }

    [TestMethod]
    public void SubWorkflow_step_action_criterion_referencing_request_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => Emit(new WorkflowOperationBinder([]), """
            {
              "stepId": "s", "workflowId": "child",
              "onSuccess": [ { "name": "go", "type": "end", "criteria": [ { "condition": "$request.path.id == '1'" } ] } ]
            }
            """));
    }

    [TestMethod]
    public void Unresolvable_reusable_action_reference_throws()
    {
        // "$components.bad" has no section.name form, so it cannot resolve.
        Should.Throw<InvalidOperationException>(() => Emit(new WorkflowOperationBinder([]), """
            {
              "stepId": "s", "workflowId": "child",
              "onFailure": [ { "reference": "$components.bad" } ]
            }
            """));
    }

    [TestMethod]
    public void Action_with_unknown_type_defaults_to_end()
    {
        // An action whose type is not goto/retry/end is treated as 'end'; emission succeeds.
        string source = Emit(new WorkflowOperationBinder([]), """
            {
              "stepId": "s", "workflowId": "child",
              "onSuccess": [ { "name": "x", "type": "mystery" } ]
            }
            """);

        source.ShouldContain("ChildWorkflow");
    }

    [TestMethod]
    public void Workflow_level_parameter_applies_to_a_step_that_omits_it()
    {
        // The step has no petId of its own; the workflow-level default supplies it.
        string source = EmitOperationWorkflow(
            """ "parameters": [ { "name": "petId", "value": "$inputs.fromWorkflow" } ], """,
            string.Empty);

        source.ShouldContain("petId:");
        source.ShouldContain("fromWorkflow");
    }

    [TestMethod]
    public void Step_parameter_overrides_the_workflow_level_default()
    {
        // Both scopes bind petId; the step's value wins and the workflow default is not applied.
        string source = EmitOperationWorkflow(
            """ "parameters": [ { "name": "petId", "value": "$inputs.fromWorkflow" } ], """,
            """ , "parameters": [ { "name": "petId", "value": "$inputs.fromStep" } ] """);

        source.ShouldContain("fromStep");
        source.ShouldNotContain("fromWorkflow");
    }

    private static string EmitOperationWorkflow(string workflowParametersClause, string stepParametersClause)
    {
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

        string doc = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
              "workflows": [ { "workflowId": "adopt", {{workflowParametersClause}} "steps": [ { "stepId": "getPet", "operationId": "getPet" {{stepParametersClause}} } ], "outputs": {} } ]
            }
            """;

        using var parsed = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(doc));
        ArazzoDocument.WorkflowObject workflow = parsed.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("Acme.Pets.Workflows", "AdoptWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
    }

    private static string Emit(WorkflowOperationBinder binder, string stepJson)
    {
        string doc = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "events", "url": "./e.yaml", "type": "asyncapi" } ],
              "workflows": [ { "workflowId": "w", "steps": [ {{stepJson}} ], "outputs": {} } ]
            }
            """;
        using var parsed = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(doc));
        ArazzoDocument.WorkflowObject workflow = parsed.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("Gen", "Wf", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
    }
}
