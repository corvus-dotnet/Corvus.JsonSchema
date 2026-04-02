// <copyright file="PatchAddBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Corvus.Text.Json.Patch;
using V4PatchDocument = Corvus.Json.Patch.Model.JsonPatchDocument;
using V5PatchDocument = Corvus.Text.Json.Patch.JsonPatchDocument;

namespace Corvus.Text.Json.Benchmarks.PatchBenchmarks;

/// <summary>
/// Benchmark for RFC 6902 A.1 — Adding an Object Member.
/// </summary>
[MemoryDiagnoser]
public class PatchAddBenchmark : PatchBenchmarkBase
{
    private const string PatchJson = """[{"op":"add","path":"/baz","value":"qux"}]""";
    private const string InputJson = """{"foo":"bar"}""";

    private V4PatchDocument v4Patch;
    private V5PatchDocument v5Patch;
    private global::Json.Patch.JsonPatch? jePatch;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.jePatch = BuildJEPatch(PatchJson);
        this.v4Patch = BuildV4Patch(PatchJson);
        this.v5Patch = BuildV5Patch(PatchJson);
        this.GlobalSetupJson(InputJson);
    }

    /// <summary>
    /// V5 builder creation only — measures the overhead of creating a mutable copy.
    /// </summary>
    [Benchmark]
    public void CreateV5BuilderOnly()
    {
        using var builder = this.CreateV5Builder();
    }

    /// <summary>
    /// Patch using Corvus V5.
    /// </summary>
    [Benchmark]
    public bool PatchCorvusV5()
    {
        using var builder = this.CreateV5Builder();
        JsonElement.Mutable target = builder.RootElement;
        return target.TryApplyPatch(in this.v5Patch);
    }

    /// <summary>
    /// Patch using Corvus V4.
    /// </summary>
    [Benchmark]
    public bool PatchCorvusV4()
    {
        return Corvus.Json.Patch.JsonPatchExtensions.TryApplyPatch(this.V4Any, this.v4Patch, out _);
    }

    /// <summary>
    /// Patch using JsonEverything.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool PatchJsonEverything()
    {
        global::Json.Patch.PatchResult? result = this.jePatch?.Apply(this.ElementAsNode());
        return result?.IsSuccess ?? false;
    }
}