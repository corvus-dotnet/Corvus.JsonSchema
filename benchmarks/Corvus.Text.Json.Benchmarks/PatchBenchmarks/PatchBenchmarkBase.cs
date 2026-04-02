// <copyright file="PatchBenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json.Patch;
using V4PatchDocument = Corvus.Json.Patch.Model.JsonPatchDocument;
using V5PatchDocument = Corvus.Text.Json.Patch.JsonPatchDocument;

namespace Corvus.Text.Json.Benchmarks.PatchBenchmarks;

/// <summary>
/// Base class for JSON Patch benchmarks comparing V4, V5, and JsonEverything implementations.
/// </summary>
public class PatchBenchmarkBase
{
    private System.Text.Json.Nodes.JsonNode? node;
    private JsonWorkspace? workspace;
    private string? inputJsonString;

    /// <summary>
    /// Gets the V4 JsonAny for the benchmark.
    /// </summary>
    public Corvus.Json.JsonAny V4Any { get; private set; }

    /// <summary>
    /// Gets the V5 ParsedJsonDocument for the benchmark.
    /// </summary>
    public ParsedJsonDocument<JsonElement>? V5Parsed { get; private set; }

    /// <summary>
    /// Builds a JsonEverything patch from a JSON string.
    /// </summary>
    protected static global::Json.Patch.JsonPatch BuildJEPatch(string patch)
    {
        return JsonSerializer.Deserialize<global::Json.Patch.JsonPatch>(patch)!;
    }

    /// <summary>
    /// Builds a V4 patch from a JSON string.
    /// </summary>
    protected static V4PatchDocument BuildV4Patch(string patch)
    {
        return V4PatchDocument.Parse(patch);
    }

    /// <summary>
    /// Builds a V5 patch from a JSON string.
    /// </summary>
    protected static V5PatchDocument BuildV5Patch(string patch)
    {
        return V5PatchDocument.ParseValue(patch);
    }

    /// <summary>
    /// Set up the benchmark using a JSON string.
    /// </summary>
    protected void GlobalSetupJson(string jsonString)
    {
        this.inputJsonString = jsonString;
        this.node = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
        this.V4Any = Corvus.Json.JsonAny.Parse(jsonString);
        this.V5Parsed = ParsedJsonDocument<JsonElement>.Parse(jsonString);
        this.workspace = JsonWorkspace.Create();
    }

    /// <summary>
    /// Set up the benchmark using a file.
    /// </summary>
    protected void GlobalSetupFile(string filePath)
    {
        this.inputJsonString = File.ReadAllText(filePath);

        using Stream stream1 = File.OpenRead(filePath);
        this.node = System.Text.Json.Nodes.JsonNode.Parse(stream1);

        using Stream stream2 = File.OpenRead(filePath);
        this.V4Any = Corvus.Json.JsonAny.Parse(stream2);

        this.V5Parsed = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(filePath));
        this.workspace = JsonWorkspace.Create();
    }

    /// <summary>
    /// Creates a fresh V5 mutable builder from the pre-parsed immutable document.
    /// </summary>
    protected JsonDocumentBuilder<JsonElement.Mutable> CreateV5Builder()
    {
        return this.V5Parsed!.RootElement.CreateBuilder(this.workspace!);
    }

    /// <summary>
    /// Gets the element as a JsonNode. For destructive operations (remove, move),
    /// use <see cref="ParseFreshNode"/> to get a clean copy each invocation.
    /// </summary>
    protected System.Text.Json.Nodes.JsonNode? ElementAsNode()
    {
        return this.node;
    }

    /// <summary>
    /// Parses a fresh JsonNode from the stored input JSON. Use this for destructive
    /// operations where the node is mutated by JsonEverything's Apply method.
    /// </summary>
    protected System.Text.Json.Nodes.JsonNode? ParseFreshNode()
    {
        return System.Text.Json.Nodes.JsonNode.Parse(this.inputJsonString!);
    }
}