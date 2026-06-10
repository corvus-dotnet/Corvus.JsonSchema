// <copyright file="WorkflowExecutorComponentTests.cs" company="Endjin Limited">
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
/// Proves reusable <c>$ref</c> success/failure actions (resolved from <c>components</c>) and
/// workflow-level <c>failureActions</c>/<c>successActions</c> (applied as per-step defaults) compile and run.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string ReusableActionDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "components": {
            "failureActions": {
              "retry5xx": { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 2, "criteria": [ { "condition": "$statusCode == 500" } ] }
            }
          },
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "reference": "$components.failureActions.retry5xx" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string WorkflowLevelActionDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "failureActions": [
                { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 2, "criteria": [ { "condition": "$statusCode == 500" } ] }
              ],
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_resolves_a_reusable_ref_failure_action()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(ReusableActionDocument);
        await RetriesThenSucceeds(assembly, "GeneratedWorkflows.Workflows.AdoptWorkflow");
    }

    [TestMethod]
    public async Task Generated_executor_applies_a_workflow_level_failure_action_to_a_step()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(WorkflowLevelActionDocument);
        await RetriesThenSucceeds(assembly, "GeneratedWorkflows.Workflows.AdoptWorkflow");
    }

    [TestMethod]
    public void Emit_throws_for_an_unresolved_reusable_action_reference()
    {
        // EmitGetPetExecutor supplies no components, so a $ref cannot resolve.
        const string danglingRef = """
            {
              "arazzo": "1.0.1",
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
                      "successCriteria": [ { "condition": "$statusCode == 200" } ],
                      "onFailure": [ { "reference": "$components.failureActions.missing" } ]
                    }
                  ]
                }
              ]
            }
            """;

        Should.Throw<InvalidOperationException>(() => EmitGetPetExecutor(danglingRef, "AdoptWorkflow"))
            .Message.ShouldContain("$components.failureActions.missing");
    }

    private const string ReusableParameterDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "components": {
            "parameters": {
              "petIdParam": { "name": "petId", "in": "path", "value": "$inputs.petId" }
            }
          },
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "reference": "$components.parameters.petIdParam" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string ReusableParameterOverrideDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "components": {
            "parameters": {
              "petIdParam": { "name": "petId", "in": "path", "value": "$inputs.petId" }
            }
          },
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "reference": "$components.parameters.petIdParam", "value": "99" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_resolves_a_reusable_ref_parameter()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(ReusableParameterDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.AdoptWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // petId came from the component parameter's value ($inputs.petId).
        transport.Requests[0].Path.ShouldBe("/pets/42");
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    [TestMethod]
    public async Task A_reusable_ref_parameter_value_is_overridden_on_the_reference()
    {
        Assembly assembly = await GenerateAndCompileWorkflows(ReusableParameterOverrideDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.AdoptWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        await pending;

        // The reference overrode the component's value with the literal "99".
        transport.Requests[0].Path.ShouldBe("/pets/99");
    }

    [TestMethod]
    public void Emit_throws_for_an_unresolved_reusable_parameter_reference()
    {
        const string danglingParam = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
              "workflows": [
                {
                  "workflowId": "adopt",
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "reference": "$components.parameters.missing" } ],
                      "successCriteria": [ { "condition": "$statusCode == 200" } ]
                    }
                  ]
                }
              ]
            }
            """;

        Should.Throw<InvalidOperationException>(() => EmitGetPetExecutor(danglingParam, "AdoptWorkflow"))
            .Message.ShouldContain("$components.parameters.missing");
    }

    private static async Task RetriesThenSucceeds(Assembly assembly, string typeName)
    {
        MethodInfo execute = assembly.GetType(typeName)!.GetMethod("ExecuteAsync")!;

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
}