// <copyright file="WorkflowExecutorSubWorkflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Proves a <c>workflowId</c> step compiles and runs: it invokes the target workflow's generated
/// executor with an inputs object built from the step's parameters and surfaces its outputs.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string ParentSubWorkflowDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            }
          ]
        }
        """;

    private const string ParentAndChildDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "petName": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public void Emits_a_sub_workflow_step_as_a_call_to_the_target_executor()
    {
        string source = EmitGetPetExecutor(ParentSubWorkflowDocument, "ParentWorkflow");

        // The parameters are projected into an inputs object and handed to the child's ExecuteAsync; its
        // result becomes the step's outputs (read by the workflow output via $steps.callChild.outputs).
        source.ShouldContain("builder.AddProperty(\"petId\"u8, values[0]);");
        source.ShouldContain("JsonElement callChildOutputsElement = await GeneratedWorkflows.ChildWorkflow.ExecuteAsync(transport, workspace,");
        source.ShouldContain("callChildOutputsElement.TryGetProperty(\"petName\"u8");
    }

    [TestMethod]
    public async Task Generated_parent_invokes_a_sub_workflow_and_surfaces_its_outputs()
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                typeof(PetByIdRequest).FullName!,
                typeof(PetByIdResponse).FullName!,
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(PetByIdClient).FullName!,
                "GetPetAsync",
                null,
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(ParentAndChildDocument), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));

        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];
        files.Count(f => f.FileName.EndsWith("Workflow.cs", StringComparison.Ordinal)).ShouldBe(2);

        Assembly assembly = CompileInMemory(executors);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // The parent passed petId to the child, which fetched the pet; the child's petName output flows
        // back up as the parent's name output.
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets/42");
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    private const string RetrySubWorkflowDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ],
                  "onFailure": [ { "name": "retryChild", "type": "retry", "retryAfter": 0, "retryLimit": 2 } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "petName": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string GotoWorkflowDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "step1",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onSuccess": [ { "name": "handOff", "type": "goto", "workflowId": "other" } ]
                }
              ],
              "outputs": {}
            },
            {
              "workflowId": "other",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "tag": "$response.body#/name" }
                }
              ],
              "outputs": { "tag": "$steps.getPet.outputs.tag" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_parent_retries_a_failing_sub_workflow_step()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(RetrySubWorkflowDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        // The child fails the first time (its getPet returns 500 → WorkflowStepFailedException); the
        // parent's onFailure retry re-invokes it and the second attempt (200) succeeds.
        var transport = new MockApiTransport();
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        transport.Requests.Count.ShouldBe(2);
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    [TestMethod]
    public async Task Generated_workflow_transfers_to_another_workflow_on_a_goto_action()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(GotoWorkflowDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // step1 succeeds and transfers to 'other', whose outputs become the result — so the result has
        // 'other''s 'tag' output, not the (empty) parent outputs.
        transport.Requests.Count.ShouldBe(2);
        outputs.TryGetProperty("tag"u8, out JsonElement tag).ShouldBeTrue();
        tag.GetString().ShouldBe("Fido");
    }

    private const string WorkflowsCriterionDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$workflows.child.outputs.petName == 'Fido'" } ],
                  "onSuccess": [ { "name": "done", "type": "end" } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "petName": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_parent_evaluates_a_workflows_outputs_criterion_against_a_sub_workflow()
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                typeof(PetByIdRequest).FullName!,
                typeof(PetByIdResponse).FullName!,
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(PetByIdClient).FullName!,
                "GetPetAsync",
                null,
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(WorkflowsCriterionDocument), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));

        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];

        // The parent exposes the sub-workflow's outputs so its $workflows.child.outputs criterion resolves.
        executors.Any(s => s.Contains("context.SetWorkflowOutputs(\"child\"", StringComparison.Ordinal)).ShouldBeTrue();

        Assembly assembly = CompileInMemory(executors);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        // The step succeeds only if $workflows.child.outputs.petName resolves to "Fido"; on failure the
        // (action-less) failure path would throw, so a clean completion proves the expression resolved.
        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    private const string WorkflowsInputsCriterionDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$workflows.child.inputs.petId == '42'" } ],
                  "onSuccess": [ { "name": "done", "type": "end" } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "petName": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_parent_resolves_a_workflows_inputs_criterion_against_a_sub_workflow()
    {
        // The parent passes petId to the child; $workflows.child.inputs.petId must resolve to the passed
        // value ("42"). The inputs qualifier was previously never wired into the generated executor (it
        // resolved to undefined, failing the action-less step), so a clean completion proves it now
        // resolves — navigated statically from the passed-inputs local.
        Assembly assembly = await GenerateAndCompileWorkflows(WorkflowsInputsCriterionDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    private static async Task<Assembly> GenerateAndCompileWorkflows(string document)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                typeof(PetByIdRequest).FullName!,
                typeof(PetByIdResponse).FullName!,
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(PetByIdClient).FullName!,
                "GetPetAsync",
                null,
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(document), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));

        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];
        return CompileInMemory(executors);
    }
}