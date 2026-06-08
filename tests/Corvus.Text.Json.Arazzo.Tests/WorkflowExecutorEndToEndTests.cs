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
                null),
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

    private static Assembly CompileInMemory(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));

        // Force-load assemblies the emitted code references transitively (e.g. NodaTime, via an
        // ObjectBuilder.AddProperty overload) so they appear in the loaded-assembly reference set.
        _ = typeof(NodaTime.OffsetTime).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratedWorkflows.Tests",
            [tree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

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