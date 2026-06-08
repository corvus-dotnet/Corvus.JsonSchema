// <copyright file="WorkflowExecutorDependsOnTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
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

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
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
}