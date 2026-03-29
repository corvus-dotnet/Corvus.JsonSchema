// <copyright file="BenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;

namespace Benchmarks;

/// <summary>
/// Base class for benchmarks.
/// </summary>
public class BenchmarkBase
{
    private System.Text.Json.Nodes.JsonNode? node;

    /// <summary>
    /// Gets the JsonAny for the benchmark.
    /// </summary>
    public JsonAny Any { get; private set; }

    /// <summary>
    /// Builds a JE patch from a JSON string.
    /// </summary>
    /// <param name="patch">The JSON string containing the patch to use.</param>
    /// <returns>The <see cref="Json.Patch.JsonPatch"/> built from the string.</returns>
    protected static Json.Patch.JsonPatch BuildJEPatch(string patch)
    {
        return JsonSerializer.Deserialize<Json.Patch.JsonPatch>(patch)!;
    }

    /// <summary>
    /// Set up the benchmark using the given file.
    /// </summary>
    /// <param name="filePath">The input file for the document to transform.</param>
    /// <returns>A <see cref="Task"/> which completes when setup is complete.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input file could not be parsed.</exception>
    protected Task GlobalSetup(string filePath)
    {
        using Stream stream1 = File.OpenRead(filePath);
        this.node = System.Text.Json.Nodes.JsonNode.Parse(stream1);

        using Stream stream2 = File.OpenRead(filePath);
        this.Any = JsonAny.Parse(stream2);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Set up the benchmark using the given json string.
    /// </summary>
    /// <param name="jsonString">The input json for the document to transform.</param>
    /// <returns>A <see cref="Task"/> which completes when setup is complete.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input file could not be parsed.</exception>
    protected Task GlobalSetupJson(string jsonString)
    {
        this.node = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
        this.Any = JsonAny.Parse(jsonString);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the element as a JsonNode.
    /// </summary>
    /// <returns>The json node for the element.</returns>
    protected System.Text.Json.Nodes.JsonNode? ElementAsNode()
    {
        return this.node;
    }
}