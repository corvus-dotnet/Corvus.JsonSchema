// <copyright file="PatchMultiOperationBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Corvus.Text.Json.Patch;
using V4PatchDocument = Corvus.Json.Patch.Model.JsonPatchDocument;
using V5PatchDocument = Corvus.Text.Json.Patch.JsonPatchDocument;

namespace Corvus.Text.Json.Benchmarks.PatchBenchmarks;

/// <summary>
/// Benchmark for a compound patch with multiple operation types (add, replace, move, copy, remove).
/// </summary>
[MemoryDiagnoser]
public class PatchMultiOperationBenchmark : PatchBenchmarkBase
{
    private const string PatchJson = """
        [
            {"op":"add","path":"/newProp","value":"hello"},
            {"op":"replace","path":"/foo","value":99},
            {"op":"copy","from":"/foo","path":"/fooCopy"},
            {"op":"move","from":"/bar","path":"/movedBar"},
            {"op":"remove","path":"/baz"},
            {"op":"add","path":"/arr/-","value":4},
            {"op":"replace","path":"/arr/0","value":100}
        ]
        """;

    private const string InputJson = """{"foo":1,"bar":"hello","baz":true,"arr":[1,2,3]}""";

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
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.GlobalCleanupV5();

    /// <summary>
    /// Patch using Corvus V5.
    /// </summary>
    [Benchmark]
    public bool PatchCorvusV5()
    {
        JsonElement.Mutable target = this.RestoreV5Builder();
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
        global::Json.Patch.PatchResult? result = this.jePatch?.Apply(this.ParseFreshNode());
        return result?.IsSuccess ?? false;
    }
}