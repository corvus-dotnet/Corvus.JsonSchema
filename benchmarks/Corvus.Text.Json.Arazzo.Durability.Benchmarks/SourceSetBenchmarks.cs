// <copyright file="SourceSetBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the deferred <see cref="SourceSet"/> holder against the <see cref="List{T}"/>-of-<see cref="CatalogSourceRef"/>
/// shape it replaces, op by op. The baselines are the code each op displaced: a System.Text.Json round-trip through a
/// DTO list (the former Azure catalog codec) and enumerating a parsed element into a list (the former JSON/SQL read).
/// </summary>
public class SourceSetBenchmarks
{
    private CatalogSourceRef[] sources = null!;
    private string jsonString = null!;
    private SourceSet sourceSet;
    private List<SourceDto> dtoList = null!;
    private ParsedJsonDocument<JsonElement> document;
    private JsonElement sourcesElement;

    [GlobalSetup]
    public void Setup()
    {
        this.sources = [new("petstore", "openapi"), new("orders", "openapi"), new("inventory", "asyncapi")];
        this.sourceSet = SourceSet.FromSources(this.sources);
        this.jsonString = this.sourceSet.ToJsonStringOrNull()!;
        this.dtoList = [new("petstore", "openapi"), new("orders", "openapi"), new("inventory", "asyncapi")];
        this.document = ParsedJsonDocument<JsonElement>.Parse(("{\"sources\":" + this.jsonString + "}").AsMemory());
        this.sourcesElement = this.document.RootElement.GetProperty("sources"u8);
    }

    [GlobalCleanup]
    public void Cleanup() => this.document.Dispose();

    // ---- write leaf (package projection / Azure column) ----

    /// <summary>Old: serialize a DTO list to the JSON column with System.Text.Json.</summary>
    /// <returns>The JSON string.</returns>
    [Benchmark(Baseline = true)]
    public string Old_Stj_Serialize() => System.Text.Json.JsonSerializer.Serialize(this.dtoList);

    /// <summary>New: build the holder from source refs (one owned array).</summary>
    /// <returns>The source count.</returns>
    [Benchmark]
    public int New_SourceSet_FromSources() => SourceSet.FromSources(this.sources).Count;

    /// <summary>New: serialize the holder to its JSON column string (no STJ).</summary>
    /// <returns>The JSON string.</returns>
    [Benchmark]
    public string? New_SourceSet_ToJsonString() => this.sourceSet.ToJsonStringOrNull();

    // ---- read leaf (column / document) ----

    /// <summary>Old: deserialize the JSON column into a DTO list with System.Text.Json.</summary>
    /// <returns>The source count.</returns>
    [Benchmark]
    public int Old_Stj_Deserialize() => System.Text.Json.JsonSerializer.Deserialize<List<SourceDto>>(this.jsonString)!.Count;

    /// <summary>New: build the holder from the JSON column string (one owned array, no STJ).</summary>
    /// <returns>The source count.</returns>
    [Benchmark]
    public int New_SourceSet_FromJsonString() => SourceSet.FromJsonStringOrEmpty(this.jsonString).Count;

    /// <summary>Old: enumerate the parsed sources element into a list of refs.</summary>
    /// <returns>The source count.</returns>
    [Benchmark]
    public int Old_Element_Enumerate()
    {
        var list = new List<CatalogSourceRef>();
        foreach (JsonElement source in this.sourcesElement.EnumerateArray())
        {
            string name = source.GetProperty("name"u8).GetString() ?? string.Empty;
            string? type = source.TryGetProperty("type"u8, out JsonElement t) ? t.GetString() : null;
            list.Add(new CatalogSourceRef(name, type));
        }

        return list.Count;
    }

    /// <summary>New: copy the parsed sources element's canonical bytes into the holder (one owned array).</summary>
    /// <returns>The source count.</returns>
    [Benchmark]
    public int New_SourceSet_CopyFrom() => SourceSet.CopyFrom(this.sourcesElement).Count;

    /// <summary>The DTO shape the former Azure catalog codec round-tripped through System.Text.Json.</summary>
    /// <param name="Name">The source name.</param>
    /// <param name="Type">The source type.</param>
    public record SourceDto(string Name, string? Type);
}