// <copyright file="WorkflowExecutorDependsOnTests.cs" company="Endjin Limited">
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
/// Proves step-level <c>dependsOn</c> (Arazzo 1.1) reorders execution so each step's declared
/// dependencies run first, regardless of document order, and that a cycle is rejected.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    // stepA is listed first but dependsOn stepB, so stepB (/pets/2) must run before stepA (/pets/1).
    private const string DependsOnDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "ordered",
              "steps": [
                {
                  "stepId": "stepA",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "1" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "dependsOn": [ "stepB" ]
                },
                {
                  "stepId": "stepB",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "2" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    private const string DependsOnCycleDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "cyclic",
              "steps": [
                {
                  "stepId": "stepA",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "1" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "dependsOn": [ "stepB" ]
                },
                {
                  "stepId": "stepB",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "2" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "dependsOn": [ "stepA" ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_orders_steps_by_dependsOn()
    {
        string source = EmitGetPetExecutor(DependsOnDocument, "OrderedWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.OrderedWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        await pending;

        // Despite stepA being declared first, stepB ran first because stepA dependsOn stepB.
        transport.Requests.Count.ShouldBe(2);
        transport.Requests[0].Path.ShouldBe("/pets/2");
        transport.Requests[1].Path.ShouldBe("/pets/1");
    }

    [TestMethod]
    public void Emit_throws_for_a_dependsOn_cycle()
    {
        Should.Throw<InvalidOperationException>(() => EmitGetPetExecutor(DependsOnCycleDocument, "CyclicWorkflow"))
            .Message.ShouldContain("cycle");
    }

    // §5.8.5.2.4: an output reference ($steps.<id>.outputs.<field>) is an IMPLICIT dependency the executor must honour.
    // stepX dependsOn stepY (explicit edge stepY->stepX); stepY reads $steps.stepX.outputs (implicit edge stepX->stepY).
    // The combined graph is a CYCLE and must be diagnosed, not silently mis-ordered into a forward, undefined reference.
    private const string ImplicitCycleDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "cyclicImplicit",
              "steps": [
                {
                  "stepId": "stepX",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "1" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "dependsOn": [ "stepY" ],
                  "outputs": { "petName": "$response.body#/name" }
                },
                {
                  "stepId": "stepY",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "2" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "result": "$steps.stepX.outputs.petName" }
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    // §5.8.5.2.4: with NO explicit dependsOn anywhere, an output reference is STILL an implicit dependency the executor
    // must order around. stepEarly is declared first but reads $steps.stepLate.outputs, so stepLate must run first.
    private const string ImplicitReorderDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "implicitOrdered",
              "steps": [
                {
                  "stepId": "stepEarly",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "1" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "borrowed": "$steps.stepLate.outputs.petName" }
                },
                {
                  "stepId": "stepLate",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "2" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public void Emit_throws_for_an_implicit_output_reference_cycle()
    {
        // stepX dependsOn stepY (explicit) AND stepY reads $steps.stepX.outputs (implicit) — the combined graph is cyclic.
        Should.Throw<InvalidOperationException>(() => EmitGetPetExecutor(ImplicitCycleDocument, "CyclicImplicitWorkflow"))
            .Message.ShouldContain("cycle");
    }

    [TestMethod]
    public async Task Generated_executor_orders_steps_by_an_implicit_output_reference()
    {
        string source = EmitGetPetExecutor(ImplicitReorderDocument, "ImplicitOrderedWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ImplicitOrderedWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        await pending;

        // stepEarly is declared first, but it reads stepLate's output, so stepLate (/pets/2) must run before stepEarly (/pets/1).
        transport.Requests.Count.ShouldBe(2);
        transport.Requests[0].Path.ShouldBe("/pets/2");
        transport.Requests[1].Path.ShouldBe("/pets/1");
    }

    private const string WorkflowDependsOnDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "b",
              "dependsOn": [ "a" ],
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            },
            {
              "workflowId": "a",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;

    private const string WorkflowDependsOnCycleDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "a",
              "dependsOn": [ "b" ],
              "steps": [ { "stepId": "getPet", "operationId": "getPet", "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ], "successCriteria": [ { "condition": "$statusCode == 200" } ] } ]
            },
            {
              "workflowId": "b",
              "dependsOn": [ "a" ],
              "steps": [ { "stepId": "getPet", "operationId": "getPet", "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ], "successCriteria": [ { "condition": "$statusCode == 200" } ] } ]
            }
          ]
        }
        """;

    [TestMethod]
    public void Executor_surfaces_workflow_level_dependsOn_as_metadata()
    {
        // EmitGetPetExecutor emits the first workflow (b), which declares workflow-level dependsOn [a].
        string source = EmitGetPetExecutor(WorkflowDependsOnDocument, "BWorkflow");

        source.ShouldContain("public static System.Collections.Generic.IReadOnlyList<string> DependsOn { get; } = [\"a\"];");
    }

    [TestMethod]
    public async Task Document_emits_a_workflow_order_catalog_in_dependency_order()
    {
        IReadOnlyList<GeneratedModelFile> files = await GenerateWorkflowFiles(WorkflowDependsOnDocument);

        GeneratedModelFile order = files.Single(f => f.FileName == "Workflows/WorkflowOrder.cs");
        // 'b' dependsOn 'a' (and is declared first), so the catalog lists 'a' before 'b'.
        order.Content.ShouldContain("ExecutionOrder { get; } = [\"a\", \"b\"];");

        // The whole set (executors + catalog) compiles together.
        string[] sources = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];
        CompileInMemory(sources);
    }

    [TestMethod]
    public async Task Document_generation_throws_for_a_workflow_dependsOn_cycle()
    {
        (await Should.ThrowAsync<InvalidOperationException>(async () => await GenerateWorkflowFiles(WorkflowDependsOnCycleDocument)))
            .Message.ShouldContain("cycle");
    }

    private static async Task<IReadOnlyList<GeneratedModelFile>> GenerateWorkflowFiles(string document)
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
        return await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(document), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));
    }
}