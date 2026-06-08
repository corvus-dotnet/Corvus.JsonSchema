// <copyright file="WorkflowExecutorBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Benchmarks.Fakes;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Corvus.Text.Json.Arazzo.Benchmarks;

/// <summary>
/// End-to-end allocation benchmark for the <em>generated</em> workflow executor: the executor source
/// is emitted and Roslyn-compiled once in setup, then invoked per iteration against a zero-overhead
/// transport (cached response, no recording) with a reused <see cref="JsonWorkspace"/>, so the
/// measurement reflects only the executor's own per-run work.
/// </summary>
[MemoryDiagnoser]
public class WorkflowExecutorBenchmarks
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

    // Identical to Document but the step references only $statusCode / $inputs — never $response.body —
    // so the generator emits no response-body clone.
    private const string StatusOnlyDocument = """
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
                  "outputs": { "echo": "$inputs.petId" }
                }
              ],
              "outputs": { "id": "$steps.getPet.outputs.echo" }
            }
          ]
        }
        """;

    // Same operation, but the step's petId is an interpolation template "pet-{$inputs.id}" — the path
    // that currently allocates per run (template buffer + ForUnescapedString reification).
    private const string InterpolationDocument = """
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

    // A workflow whose success is decided by an inlined simple body-comparison criterion
    // ($response.body#/name == 'Fido') — evaluated directly via Comparand, no runtime interpreter.
    private const string SimpleCriteriaDocument = """
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
                  "successCriteria": [ { "condition": "$statusCode == 200" }, { "condition": "$response.body#/name == 'Fido'" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    // A workflow whose success is decided by an inlined jsonpath criterion (the query is compiled
    // ahead-of-time into a sibling class; success = QueryNodes(...).Count > 0).
    private const string JsonPathDocument = """
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
                  "successCriteria": [ { "context": "$response.body", "type": "jsonpath", "condition": "$.name" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    // A control-flow workflow: an onSuccess 'end' action makes the generator emit the labelled
    // switch-loop dispatch (with hoisted step locals) instead of straight-line code — this probes the
    // control-flow codegen overhead over the straight-line baseline.
    private const string ControlFlowDocument = """
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
                  "onSuccess": [ { "name": "done", "type": "end", "criteria": [ { "condition": "$statusCode == 200" } ] } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    // A parent workflow that invokes a child sub-workflow; the child runs the operation step. Probes the
    // nested-executor invocation (child inputs object construction + child outputs surfacing).
    private const string SubWorkflowDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                { "stepId": "call", "workflowId": "child", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ],
              "outputs": { "name": "$steps.call.outputs.name" }
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
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    // A send channel step: publishes the payload on an AsyncAPI channel through the generated producer
    // (a workspace materialisation + transport publish). Probes the channel-step codegen path.
    private const string ChannelSendDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./e.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "notify",
              "steps": [
                { "stepId": "send", "channelPath": "bench", "action": "send", "requestBody": { "payload": "$inputs.petId" } }
              ],
              "outputs": { "id": "$inputs.petId" }
            }
          ]
        }
        """;

    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> execute = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeStatusOnly = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeInterpolation = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeSimpleCriteria = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeJsonPath = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeControlFlow = null!;
    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeSubWorkflow = null!;
    private Func<IApiTransport, IMessageTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> executeChannelSend = null!;
    private BenchTransport transport = null!;
    private BenchMessageTransport messageTransport = null!;
    private JsonWorkspace workspace = null!;
    private ParsedJsonDocument<JsonElement> inputsDocument = default!;
    private JsonElement inputs;

    [GlobalSetup]
    public void Setup()
    {
        this.execute = Compile(Document, "AdoptWorkflow");
        this.executeStatusOnly = Compile(StatusOnlyDocument, "StatusOnlyWorkflow");
        this.executeInterpolation = Compile(InterpolationDocument, "InterpolationWorkflow");
        this.executeSimpleCriteria = Compile(SimpleCriteriaDocument, "SimpleCriteriaWorkflow");
        this.executeJsonPath = Compile(JsonPathDocument, "JsonPathWorkflow");
        this.executeControlFlow = Compile(ControlFlowDocument, "ControlFlowWorkflow");
        this.executeSubWorkflow = CompileSubWorkflow();
        this.executeChannelSend = CompileChannelSend();

        this.transport = new BenchTransport();
        this.messageTransport = new BenchMessageTransport();
        this.workspace = JsonWorkspace.Create();
        this.inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42","id":"42"}"""));
        this.inputs = this.inputsDocument.RootElement;

        static Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> Compile(string document, string className)
        {
            Assembly assembly = CompileInMemory(EmitExecutor(document, className));
            MethodInfo method = assembly.GetType($"GeneratedWorkflows.{className}")!.GetMethod("ExecuteAsync")!;
            return method.CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.workspace.Dispose();
        this.inputsDocument.Dispose();
    }

    /// <summary>Runs the generated workflow once; the workspace is reset to reuse its buffers.</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark(Baseline = true)]
    public bool RunWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.execute(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a status-only workflow (no $response.body reference, so no body clone).</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunStatusOnlyWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeStatusOnly(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("id"u8, out _);
    }

    /// <summary>Runs a workflow whose petId is an interpolation template ("pet-{$inputs.id}").</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunInterpolationWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeInterpolation(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a workflow gated by an inlined simple body-comparison criterion.</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunSimpleCriteriaWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeSimpleCriteria(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a workflow gated by an inlined (ahead-of-time-compiled) jsonpath criterion.</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunJsonPathWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeJsonPath(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a workflow whose control flow is emitted as the labelled switch-loop (onSuccess end action).</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunControlFlowWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeControlFlow(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a parent workflow that invokes a child sub-workflow.</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunSubWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeSubWorkflow(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
    }

    /// <summary>Runs a send channel-step workflow (publishes through the generated producer).</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunChannelSendWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.executeChannelSend(this.transport, this.messageTransport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("id"u8, out _);
    }

    private static Func<IApiTransport, IMessageTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> CompileChannelSend()
    {
        var descriptor = new AsyncApiChannelDescriptor(
            "bench",
            OperationAction.Send,
            "notify",
            typeof(BenchProducer).FullName!,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("bench", "Corvus.Text.Json.JsonElement", null, null, "PublishBenchAsync")]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelSendDocument));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        string source = WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("GeneratedWorkflows", "NotifyWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));

        Assembly assembly = CompileInMemory(source);
        MethodInfo method = assembly.GetType("GeneratedWorkflows.NotifyWorkflow")!.GetMethod("ExecuteAsync")!;
        return method.CreateDelegate<Func<IApiTransport, IMessageTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();
    }

    private static Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> CompileSubWorkflow()
    {
        string parent = EmitWorkflowAt(SubWorkflowDocument, 0, "ParentWorkflow");
        string child = EmitWorkflowAt(SubWorkflowDocument, 1, "ChildWorkflow");
        Assembly assembly = CompileInMemory(parent, child);
        MethodInfo method = assembly.GetType("GeneratedWorkflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;
        return method.CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();
    }

    private static string EmitExecutor(string document, string className) => EmitWorkflowAt(document, 0, className);

    private static string EmitWorkflowAt(string document, int workflowIndex, string className)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                typeof(BenchRequest).FullName!,
                typeof(BenchResponse).FullName!,
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(BenchClient).FullName!,
                "GetPetAsync",
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        int index = 0;
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            if (index++ != workflowIndex)
            {
                continue;
            }

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

    private static Assembly CompileInMemory(params string[] sources)
    {
        // Define target-framework symbols so generated code (e.g. ahead-of-time jsonpath classes) takes
        // its modern #if NET8_0_OR_GREATER path.
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview).WithPreprocessorSymbols(
            "NET", "NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER",
            "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER");
        SyntaxTree[] trees = [.. sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions))];

        // Force-load assemblies the emitted code references transitively so they appear in the set.
        _ = typeof(NodaTime.OffsetTime).Assembly;
        _ = typeof(System.Diagnostics.ActivitySource).Assembly;
        _ = typeof(System.Numerics.BigInteger).Assembly;
        _ = typeof(Corvus.Text.Json.JsonPath.JsonPathResult).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratedWorkflows.Bench",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));

        using var peStream = new MemoryStream();
        EmitResult result = compilation.Emit(peStream);
        if (!result.Success)
        {
            string errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Generated executor failed to compile:{Environment.NewLine}{errors}");
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }
}