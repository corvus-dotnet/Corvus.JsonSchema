// <copyright file="MergePatchBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Patch;

#if !NETFRAMEWORK
using StjJsonDocument = System.Text.Json.JsonDocument;
#endif

namespace Corvus.Text.Json.Benchmarks.MergePatchBenchmarks;

/// <summary>
/// Benchmarks for RFC 7396 JSON Merge Patch comparing Corvus V5 against
/// JsonCons.Utilities (the most established STJ-based merge patch library).
/// </summary>
[MemoryDiagnoser]
public class MergePatchBenchmark
{
    private const string TargetJson = """
        {
            "title": "Goodbye!",
            "author": { "givenName": "John", "familyName": "Doe" },
            "tags": ["example", "sample"],
            "content": "This will be unchanged"
        }
        """;

    private const string PatchJson = """
        {
            "title": "Hello!",
            "author": { "familyName": null },
            "tags": ["example"],
            "phoneNumber": "+01-123-456-7890"
        }
        """;

    private ParsedJsonDocument<JsonElement>? targetParsed;
    private JsonElement patchElement;
    private ParsedJsonDocument<JsonElement>? patchParsed;
    private JsonWorkspace? workspace;
    private JsonDocumentBuilder<JsonElement.Mutable>? builder;
    private JsonDocumentBuilderSnapshot<JsonElement.Mutable>? snapshot;
#if !NETFRAMEWORK
    private StjJsonDocument? targetStjDoc;
    private StjJsonDocument? patchStjDoc;
#endif

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.targetParsed = ParsedJsonDocument<JsonElement>.Parse(TargetJson);
        this.patchParsed = ParsedJsonDocument<JsonElement>.Parse(PatchJson);
        this.patchElement = this.patchParsed.RootElement;

        this.workspace = JsonWorkspace.CreateUnrented();
        this.builder = this.targetParsed.RootElement.CreateBuilder(this.workspace);
        this.snapshot = this.builder.CreateSnapshot();

#if !NETFRAMEWORK
        this.targetStjDoc = StjJsonDocument.Parse(TargetJson);
        this.patchStjDoc = StjJsonDocument.Parse(PatchJson);
#endif
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.snapshot?.Dispose();
        this.builder?.Dispose();
        this.workspace?.Dispose();
        this.patchParsed?.Dispose();
        this.targetParsed?.Dispose();
#if !NETFRAMEWORK
        this.patchStjDoc?.Dispose();
        this.targetStjDoc?.Dispose();
#endif
    }

    /// <summary>
    /// Merge patch using Corvus V5.
    /// </summary>
    [Benchmark]
    public int CorvusV5()
    {
        this.builder!.Restore(this.snapshot!);
        JsonElement.Mutable target = this.builder.RootElement;
        JsonMergePatchExtensions.ApplyMergePatch(ref target, in this.patchElement);
        return (int)target.ValueKind;
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Merge patch using JsonCons.Utilities.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int JsonConsUtilities()
    {
        using StjJsonDocument result = JsonCons.Utilities.JsonMergePatch.ApplyMergePatch(
            this.targetStjDoc!.RootElement,
            this.patchStjDoc!.RootElement);
        return (int)result.RootElement.ValueKind;
    }
#endif
}
