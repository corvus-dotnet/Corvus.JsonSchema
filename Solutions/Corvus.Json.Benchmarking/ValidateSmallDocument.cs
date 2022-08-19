// <copyright file="ValidateSmallDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Corvus.Json.Benchmarking.Models;
using JsonEverything = global::Json.Schema;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class ValidateSmallDocument
{
    private const string JsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": []
    },
    ""dateOfBirth"": ""1944-07-14""
}";

    private static readonly JsonEverything.ValidationOptions Options = new JsonEverything.ValidationOptions() { OutputFormat = JsonEverything.OutputFormat.Flag };

    private JsonDocument? objectDocument;
    private Person person;
    private JsonNode? node;
    private JsonEverything.JsonSchema? schema;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalSetup]
    public Task GlobalSetup()
    {
        this.objectDocument = JsonDocument.Parse(JsonText);
        this.person = Person.FromJson(this.objectDocument.RootElement);
        this.schema = JsonEverything.JsonSchema.FromFile("./PersonModel/person-schema.json");
        this.node = System.Text.Json.Nodes.JsonObject.Create(this.person.AsJsonElement.Clone());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalCleanup]
    public Task GlobalCleanup()
    {
        if (this.objectDocument is JsonDocument doc)
        {
            doc.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ValidateSmallDocumentCorvus()
    {
        ValidationContext result = this.person.Validate(ValidationContext.ValidContext);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ValidateSmallDocumentJsonEveything()
    {
        JsonEverything.ValidationResults result = this.schema!.Validate(this.node, Options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }
}