// <copyright file="WorkflowExecutorBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Benchmarks.Fakes;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo10;
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

    private Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>> execute = null!;
    private BenchTransport transport = null!;
    private JsonWorkspace workspace = null!;
    private ParsedJsonDocument<JsonElement> inputsDocument = default!;
    private JsonElement inputs;

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = CompileInMemory(EmitExecutor());
        MethodInfo method = assembly.GetType("GeneratedWorkflows.AdoptWorkflow")!.GetMethod("ExecuteAsync")!;
        this.execute = method.CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();

        this.transport = new BenchTransport();
        this.workspace = JsonWorkspace.Create();
        this.inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        this.inputs = this.inputsDocument.RootElement;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.workspace.Dispose();
        this.inputsDocument.Dispose();
    }

    /// <summary>Runs the generated workflow once; the workspace is reset to reuse its buffers.</summary>
    /// <returns>Whether the workflow produced the expected output (a sink for the probe).</returns>
    [Benchmark]
    public bool RunWorkflow()
    {
        this.workspace.Reset();
        ValueTask<JsonElement> pending = this.execute(this.transport, this.workspace, this.inputs, default);
        JsonElement result = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
        return result.TryGetProperty("name"u8, out _);
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
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        // Force-load assemblies the emitted code references transitively so they appear in the set.
        _ = typeof(NodaTime.OffsetTime).Assembly;
        _ = typeof(System.Diagnostics.ActivitySource).Assembly;
        _ = typeof(System.Numerics.BigInteger).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratedWorkflows.Bench",
            [tree],
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