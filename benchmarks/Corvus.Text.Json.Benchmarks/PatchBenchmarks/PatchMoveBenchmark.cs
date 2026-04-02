// <copyright file="PatchMoveBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Corvus.Text.Json.Patch;
using V4PatchDocument = Corvus.Json.Patch.Model.JsonPatchDocument;
using V5PatchDocument = Corvus.Text.Json.Patch.JsonPatchDocument;

namespace Corvus.Text.Json.Benchmarks.PatchBenchmarks;

/// <summary>
/// Benchmark for RFC 6902 A.6 — Moving a Value.
/// </summary>
[MemoryDiagnoser]
public class PatchMoveBenchmark : PatchBenchmarkBase
{
    private const string PatchJson = """[{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]""";
    private const string InputJson = """{"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}""";

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