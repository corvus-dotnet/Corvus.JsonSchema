// <copyright file="ArazzoRuntimeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Benchmarks;

/// <summary>
/// Per-evaluation hot-path benchmarks for the Arazzo runtime library. The intent is to verify
/// zero (or minimal) allocation per call once criteria/expressions are compiled.
/// </summary>
[MemoryDiagnoser]
public class ArazzoRuntimeBenchmarks
{
    private static readonly byte[] BodyBytes = Encoding.UTF8.GetBytes("""{"status":"ok","count":10,"items":[1,2,3]}""");
    private static readonly byte[] LoginBytes = Encoding.UTF8.GetBytes("""{"token":"abc123"}""");

    private ParsedJsonDocument<JsonElement> bodyDoc = default!;
    private ParsedJsonDocument<JsonElement> loginDoc = default!;
    private WorkflowExecutionContext context = null!;
    private CompiledCriterion simpleNumeric = null!;
    private CompiledCriterion simpleString = null!;
    private CompiledCriterion jsonPath = null!;
    private CompiledCriterion regex = null!;
    private ArazzoExpression resolveExpression;
    private ArrayBufferWriter<byte> interpolationBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.bodyDoc = ParsedJsonDocument<JsonElement>.Parse(BodyBytes);
        this.loginDoc = ParsedJsonDocument<JsonElement>.Parse(LoginBytes);

        this.context = new WorkflowExecutionContext();
        this.context.SetResponseBody(this.bodyDoc.RootElement);
        this.context.SetResponseStatusCode(200);
        this.context.SetStepOutputs("login", this.loginDoc.RootElement);

        this.simpleNumeric = CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 200");
        this.simpleString = CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/status == 'ok'");
        this.jsonPath = CompiledCriterion.Compile(CriterionType.JsonPath, "$.items[*]", "$response.body");
        this.regex = CompiledCriterion.Compile(CriterionType.Regex, "^ok$", "$response.body#/status");
        this.resolveExpression = ArazzoExpression.Parse("$response.body#/count");
        this.interpolationBuffer = new ArrayBufferWriter<byte>(64);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bodyDoc.Dispose();
        this.loginDoc.Dispose();
    }

    [Benchmark(Description = "Resolve $response.body#/count")]
    public bool ResolveValue() => this.context.TryResolveValue(this.resolveExpression, out _);

    [Benchmark(Description = "Criterion: simple numeric ($statusCode == 200)")]
    public bool SimpleNumeric() => this.simpleNumeric.Evaluate(this.context);

    [Benchmark(Description = "Criterion: simple string (== 'ok')")]
    public bool SimpleString() => this.simpleString.Evaluate(this.context);

    [Benchmark(Description = "Criterion: jsonpath ($.items[*])")]
    public bool JsonPath() => this.jsonPath.Evaluate(this.context);

    [Benchmark(Description = "Criterion: regex (^ok$)")]
    public bool Regex() => this.regex.Evaluate(this.context);

    [Benchmark(Description = "Interpolate to UTF-8 buffer (one embedded expression)")]
    public bool Interpolate()
    {
        this.interpolationBuffer.Clear();
        return this.context.TryInterpolate("Bearer {$steps.login.outputs.token}", this.interpolationBuffer);
    }
}
