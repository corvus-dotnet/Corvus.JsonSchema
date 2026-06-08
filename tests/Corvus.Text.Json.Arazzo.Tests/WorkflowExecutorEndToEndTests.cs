// <copyright file="WorkflowExecutorEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.Arazzo10;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof that the generated executor source <em>compiles</em> and <em>runs</em>: emit a
/// workflow executor, compile it in-memory with Roslyn, then execute it against
/// <see cref="MockApiTransport"/> and assert the workflow outputs and emitted telemetry.
/// </summary>
[TestClass]
public class WorkflowExecutorEndToEndTests
{
    private const string Document = """
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
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_compiles_and_runs_against_a_mock_transport()
    {
        string source = EmitExecutor();

        Assembly assembly = CompileInMemory(source);
        Type workflowType = assembly.GetType("GeneratedWorkflows.AdoptWorkflow")
            ?? throw new InvalidOperationException("Generated workflow type not found.");
        MethodInfo execute = workflowType.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        // The workflow output `name` flows: $response.body#/name → step output petName → workflow output name.
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");

        // The step actually ran, and the run reported success.
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets/42");
        recorded.Sum("corvus.arazzo.steps.executed").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.completed").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(0);
    }

    private const string CreatePetDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "register",
              "steps": [
                {
                  "stepId": "createPet",
                  "operationId": "createPet",
                  "requestBody": { "contentType": "application/json", "payload": "$inputs.pet" },
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.createPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_binds_a_request_body_and_runs()
    {
        CreatePetClient.CapturedBodies.Clear();
        string source = EmitCreatePetExecutor();

        Assembly assembly = CompileInMemory(source);
        Type workflowType = assembly.GetType("GeneratedWorkflows.RegisterWorkflow")
            ?? throw new InvalidOperationException("Generated workflow type not found.");
        MethodInfo execute = workflowType.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Post, "/pets", 200, """{"name":"Rex"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"pet":{"name":"Rex"}}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        // The request body ($inputs.pet) was resolved and passed to the client's body parameter.
        CreatePetClient.CapturedBodies.Count.ShouldBe(1);
        CreatePetClient.CapturedBodies[0].ShouldBe("""{"name":"Rex"}""");

        // And the POST ran, returning the created pet's name through the workflow output.
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Rex");
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets");
    }

    private const string LiteralParamDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptLiteral",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "42" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string InterpolatedParamDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptInterpolated",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "pet-{$inputs.id}" } ],
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
    public async Task Generated_executor_binds_an_interpolated_parameter_value_and_runs()
    {
        string source = EmitGetPetExecutor(InterpolatedParamDocument, "AdoptInterpolatedWorkflow");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptInterpolatedWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"id":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        // "pet-{$inputs.id}" interpolated to "pet-42" and bound to the path parameter.
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets/pet-42");
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    [TestMethod]
    public async Task Generated_executor_binds_a_literal_parameter_value_and_runs()
    {
        string source = EmitLiteralParamExecutor();

        Assembly assembly = CompileInMemory(source);
        Type workflowType = assembly.GetType("GeneratedWorkflows.AdoptLiteralWorkflow")
            ?? throw new InvalidOperationException("Generated workflow type not found.");
        MethodInfo execute = workflowType.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        // The literal "42" was bound to the path parameter with no inputs involved.
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets/42");
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    private static string EmitLiteralParamExecutor() => EmitGetPetExecutor(LiteralParamDocument, "AdoptLiteralWorkflow");

    private static string EmitGetPetExecutor(string document, string className)
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
                [new ResponseHeaderInfo("X-Flag", "XFlagHeader", "string", true)]),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            return WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions(
                    "GeneratedWorkflows",
                    className,
                    "Corvus.Text.Json.JsonElement",
                    "Corvus.Text.Json.JsonElement"));
        }

        throw new InvalidOperationException("No workflow.");
    }

    private const string BooleanBodyDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "flag",
              "steps": [
                {
                  "stepId": "createPet",
                  "operationId": "createPet",
                  "requestBody": { "contentType": "application/json", "payload": true },
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_binds_a_literal_boolean_body_via_the_singleton()
    {
        CreatePetClient.CapturedBodies.Clear();
        string source = EmitCreatePetExecutor(BooleanBodyDocument, "FlagWorkflow");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.FlagWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Post, "/pets", 200, "{}");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        _ = await pending;

        // The literal `true` body was bound from the shared singleton and flowed to the client.
        CreatePetClient.CapturedBodies.Count.ShouldBe(1);
        CreatePetClient.CapturedBodies[0].ShouldBe("true");
    }

    private const string BodyCriteriaDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptChecked",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [
                    { "condition": "$statusCode == 200" },
                    { "condition": "$response.body#/status == 'ok'" },
                    { "condition": "$response.body#/count > 5" },
                    { "condition": "$response.body#/active" }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_inlines_body_comparison_success_criteria_that_pass()
    {
        string source = EmitGetPetExecutor(BodyCriteriaDocument, "AdoptCheckedWorkflow");

        // The body criteria are inlined against the live body — no CompiledCriterion, no context.
        source.ShouldContain("Comparand");
        source.ShouldNotContain("CompiledCriterion");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptCheckedWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();

        // status "OK" matches 'ok' case-insensitively; count 7 > 5; active is truthy.
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","status":"OK","count":7,"active":true}""");

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
        recorded.Sum("corvus.arazzo.workflows.completed").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(0);
    }

    [TestMethod]
    public async Task Generated_executor_inlines_body_comparison_success_criteria_that_fail()
    {
        string source = EmitGetPetExecutor(BodyCriteriaDocument, "AdoptCheckedWorkflow");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptCheckedWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();

        // count 3 is NOT > 5 — the inlined numeric comparison must fail the step.
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","status":"OK","count":3,"active":true}""");

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        // Await directly (as the other end-to-end tests do) so the continuation — and the pooled
        // workspace's dispose — stays on this thread.
        WorkflowStepFailedException? caught = null;
        try
        {
            _ = await pending;
        }
        catch (WorkflowStepFailedException ex)
        {
            caught = ex;
        }

        caught.ShouldNotBeNull();
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(1);
    }

    private const string CompoundCriteriaDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptCompound",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [
                    { "condition": "($response.body#/status == 'ok' || $response.body#/status == 'fine') && !$response.body#/error" }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_inlines_a_compound_logical_criterion()
    {
        string source = EmitGetPetExecutor(CompoundCriteriaDocument, "AdoptCompoundWorkflow");
        source.ShouldNotContain("CompiledCriterion");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptCompoundWorkflow")!.GetMethod("ExecuteAsync")!;

        // Passing: status "fine" satisfies the grouped OR, and error is false.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","status":"fine","error":false}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
            JsonElement outputs = await pending;
            outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
            name.GetString().ShouldBe("Fido");
        }

        // Failing: status is neither 'ok' nor 'fine' — the grouped OR is false, so the AND fails.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","status":"bad","error":false}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

            WorkflowStepFailedException? caught = null;
            try
            {
                _ = await pending;
            }
            catch (WorkflowStepFailedException ex)
            {
                caught = ex;
            }

            caught.ShouldNotBeNull();
        }
    }

    private static string EmitCreatePetExecutor() => EmitCreatePetExecutor(CreatePetDocument, "RegisterWorkflow");

    private static string EmitCreatePetExecutor(string document, string className)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets",
                OperationMethod.Post,
                "createPet",
                "CreatePet",
                typeof(CreatePetRequest).FullName!,
                typeof(PetByIdResponse).FullName!,
                [],
                true,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(CreatePetClient).FullName!,
                "CreatePetAsync",
                "Corvus.Text.Json.JsonElement"),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            return WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions(
                    "GeneratedWorkflows",
                    className,
                    "Corvus.Text.Json.JsonElement",
                    "Corvus.Text.Json.JsonElement"));
        }

        throw new InvalidOperationException("No workflow.");
    }

    private static string EmitExecutor()
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
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(Document));
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            return WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions(
                    "GeneratedWorkflows",
                    "AdoptWorkflow",
                    "Corvus.Text.Json.JsonElement",
                    "Corvus.Text.Json.JsonElement"));
        }

        throw new InvalidOperationException("No workflow.");
    }

    private const string RegexCriteriaDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptRegex",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [
                    { "context": "$response.body#/name", "type": "regex", "condition": "^Fi" }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_inlines_a_regex_criterion_via_generated_regex()
    {
        if (LazyRegexGenerator.Value is null)
        {
            Assert.Inconclusive("The regular-expression source generator could not be located in this environment.");
        }

        string source = EmitGetPetExecutor(RegexCriteriaDocument, "AdoptRegexWorkflow");
        source.ShouldContain("[GeneratedRegex(\"^Fi\", RegexOptions.CultureInvariant, 1000)]");
        source.ShouldNotContain("CompiledCriterion");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptRegexWorkflow")!.GetMethod("ExecuteAsync")!;

        // Passing: name "Fido" matches ^Fi.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
            JsonElement outputs = await pending;
            outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
            name.GetString().ShouldBe("Fido");
        }

        // Failing: name "Rex" does not match ^Fi.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Rex"}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

            WorkflowStepFailedException? caught = null;
            try
            {
                _ = await pending;
            }
            catch (WorkflowStepFailedException ex)
            {
                caught = ex;
            }

            caught.ShouldNotBeNull();
        }
    }

    private const string JsonPathCriteriaDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptJsonPath",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [
                    { "context": "$response.body", "type": "jsonpath", "condition": "$.tags[?@.primary == true]" }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_inlines_a_jsonpath_criterion_via_a_generated_sibling_class()
    {
        // No source generator needed: the JSONPath query is compiled ahead-of-time into a sibling class
        // baked straight into the emitted source.
        string source = EmitGetPetExecutor(JsonPathCriteriaDocument, "AdoptJsonPathWorkflow");
        source.ShouldContain("internal static class getPet_C0JsonPath");
        source.ShouldNotContain("CompiledCriterion");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptJsonPathWorkflow")!.GetMethod("ExecuteAsync")!;

        // Passing: a tag with primary == true exists.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","tags":[{"primary":false},{"primary":true}]}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
            JsonElement outputs = await pending;
            outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
            name.GetString().ShouldBe("Fido");
        }

        // Failing: no tag is primary, so the query matches nothing.
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","tags":[{"primary":false}]}""");
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

            WorkflowStepFailedException? caught = null;
            try
            {
                _ = await pending;
            }
            catch (WorkflowStepFailedException ex)
            {
                caught = ex;
            }

            caught.ShouldNotBeNull();
        }
    }

    private const string HeaderCriteriaDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptHeader",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$response.header.X-Flag == 'on'" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_inlines_a_response_header_criterion()
    {
        string source = EmitGetPetExecutor(HeaderCriteriaDocument, "AdoptHeaderWorkflow");

        // The header is read directly from the generated response property — no context.
        source.ShouldContain("getPetResponse.XFlagHeader");
        source.ShouldNotContain("WorkflowExecutionContext");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptHeaderWorkflow")!.GetMethod("ExecuteAsync")!;

        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Flag"] = "on" };

        // Passing: X-Flag is "on".
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""", headers: headers);
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
            JsonElement outputs = await pending;
            outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
            name.GetString().ShouldBe("Fido");
        }

        // Failing: X-Flag is "off".
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""", headers: new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Flag"] = "off" });
            using var workspace = JsonWorkspace.Create();
            using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

            WorkflowStepFailedException? caught = null;
            try
            {
                _ = await pending;
            }
            catch (WorkflowStepFailedException ex)
            {
                caught = ex;
            }

            caught.ShouldNotBeNull();
        }
    }

    private static readonly Lazy<IIncrementalGenerator?> LazyRegexGenerator = new(LoadRegexGenerator);

    private static IIncrementalGenerator? LoadRegexGenerator()
    {
        try
        {
            // The regex source generator ships as an analyzer in the .NET ref pack, a sibling of the
            // shared runtime directory this test runs on. Pick the highest-versioned pack available.
            string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            string dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
            string refPacks = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
            if (!Directory.Exists(refPacks))
            {
                return null;
            }

            string? dll = Directory.GetDirectories(refPacks)
                .Select(d => (Dir: d, Version: ParseVersion(Path.GetFileName(d))))
                .Where(d => d.Version is not null)
                .OrderByDescending(d => d.Version)
                .Select(d => Path.Combine(d.Dir, "analyzers", "dotnet", "cs", "System.Text.RegularExpressions.Generator.dll"))
                .FirstOrDefault(File.Exists);
            if (dll is null)
            {
                return null;
            }

            Type? type = Assembly.LoadFrom(dll).GetType("System.Text.RegularExpressions.Generator.RegexGenerator");
            return type is null ? null : Activator.CreateInstance(type) as IIncrementalGenerator;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Version? ParseVersion(string text) => Version.TryParse(text, out Version? version) ? version : null;

    private static Assembly CompileInMemory(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        // Force-load assemblies the emitted code references transitively (e.g. NodaTime, via an
        // ObjectBuilder.AddProperty overload) so they appear in the loaded-assembly reference set.
        _ = typeof(NodaTime.OffsetTime).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratedWorkflows.Tests",
            [tree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        // Run the regular-expression source generator so [GeneratedRegex] partial methods (emitted for
        // regex criteria) are completed, exactly as they would be in a consumer's compilation.
        if (LazyRegexGenerator.Value is { } regexGenerator)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [regexGenerator.AsSourceGenerator()],
                parseOptions: parseOptions);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updated, out _);
            compilation = (CSharpCompilation)updated;
        }

        using var peStream = new MemoryStream();
        EmitResult result = compilation.Emit(peStream);
        if (!result.Success)
        {
            string errors = string.Join(
                Environment.NewLine,
                result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
            Assert.Fail($"Generated executor failed to compile:{Environment.NewLine}{errors}{Environment.NewLine}--- source ---{Environment.NewLine}{source}");
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }
}