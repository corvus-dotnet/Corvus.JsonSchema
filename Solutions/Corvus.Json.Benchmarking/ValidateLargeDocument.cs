// <copyright file="ValidateLargeDocument.cs" company="Endjin Limited">
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
public class ValidateLargeDocument
{
    private const string JsonText =
        """
        {
            "name": {
              "familyName": "Oldroyd",
              "givenName": "Michael",
              "otherNames": [],
              "email": "michael.oldryoyd@contoso.com"
            },
            "dateOfBirth": "1944-07-14",
            "netWorth": 1234567890.1234567891,
            "height": 1.8
        }
        """;

    private static readonly JsonEverything.EvaluationOptions Options = new() { OutputFormat = JsonEverything.OutputFormat.Flag };

    private JsonDocument? objectDocument;
    private PersonArray personArray;
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

        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        for (int i = 0; i < 10000; ++i)
        {
            builder.Add(Person.FromJson(this.objectDocument.RootElement));
        }

        this.personArray = PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();

        this.schema = JsonEverything.JsonSchema.FromFile("./PersonModel/person-array-schema.json");
        this.node = System.Text.Json.Nodes.JsonArray.Create(this.personArray.AsJsonElement.Clone());
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
    [Benchmark(Baseline = true)]
    public void ValidateLargeArrayCorvus()
    {
        ValidationContext result = this.personArray.Validate(ValidationContext.ValidContext);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ValidateLargeArrayJsonEverything()
    {
        JsonEverything.EvaluationResults result = this.schema!.Evaluate(this.node, Options);
        if (!result.IsValid && result.Errors is IReadOnlyDictionary<string, string> errors)
        {
            foreach (KeyValuePair<string, string> r in errors)
            {
                Console.WriteLine(r.Value);
            }
        }
    }
}