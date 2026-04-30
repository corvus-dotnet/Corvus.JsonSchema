// <copyright file="JsonDiffBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Patch;

using JsonPatchNet = global::Json.Patch.JsonPatch;
using JsonPatchNetExtensions = global::Json.Patch.PatchExtensions;

namespace Corvus.Text.Json.Benchmarks.DiffBenchmarks;

/// <summary>
/// Benchmarks for JSON Diff (RFC 6902 patch generation) comparing
/// Corvus V5 against JsonPatch.Net (json-everything).
/// </summary>
[MemoryDiagnoser]
public class JsonDiffBenchmark
{
    private const string SourceJson = """
        {
            "firstName": "John",
            "lastName": "Doe",
            "age": 30,
            "address": {
                "street": "123 Main St",
                "city": "Springfield",
                "state": "IL",
                "zip": "62701"
            },
            "phones": ["+1-555-0100", "+1-555-0101"],
            "active": true,
            "notes": null
        }
        """;

    private const string TargetJson = """
        {
            "firstName": "John",
            "lastName": "Smith",
            "age": 31,
            "address": {
                "street": "456 Oak Ave",
                "city": "Springfield",
                "state": "IL",
                "zip": "62702"
            },
            "phones": ["+1-555-0100", "+1-555-0101"],
            "active": false,
            "email": "john.smith@example.com"
        }
        """;

    private ParsedJsonDocument<JsonElement>? sourceParsed;
    private ParsedJsonDocument<JsonElement>? targetParsed;
    private JsonElement sourceElement;
    private JsonElement targetElement;
    private JsonNode? sourceNode;
    private JsonNode? targetNode;
    private JsonWorkspace workspace;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.sourceParsed = ParsedJsonDocument<JsonElement>.Parse(SourceJson);
        this.targetParsed = ParsedJsonDocument<JsonElement>.Parse(TargetJson);
        this.sourceElement = this.sourceParsed.RootElement;
        this.targetElement = this.targetParsed.RootElement;

        this.sourceNode = JsonNode.Parse(SourceJson);
        this.targetNode = JsonNode.Parse(TargetJson);

        this.workspace = JsonWorkspace.CreateUnrented();
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.workspace.Dispose();
        this.targetParsed?.Dispose();
        this.sourceParsed?.Dispose();
    }

    /// <summary>
    /// Diff using Corvus V5.
    /// </summary>
    [Benchmark]
    public int CorvusV5()
    {
        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(this.sourceElement, this.targetElement, this.workspace);
        int count = patch.GetArrayLength();
        this.workspace.Reset();
        return count;
    }

    /// <summary>
    /// Diff using JsonPatch.Net (json-everything).
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonPatchNet JsonPatchNetBenchmark()
    {
        return JsonPatchNetExtensions.CreatePatch(this.sourceNode, this.targetNode);
    }
}
