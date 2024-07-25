// <copyright file="ValidateLargeDocumentWithAnnotationCollection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using JsonEverything = global::Json.Schema;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class ValidateLargeDocumentWithAnnotationCollection
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

    private static readonly JsonEverything.EvaluationOptions Options = new() { OutputFormat = JsonEverything.OutputFormat.List };

    private JsonDocument? objectDocument;
    private Models.V2.PersonArray personArrayV2;
    private Models.V3.PersonArray personArrayV3;
    private JsonNode? node;
    private JsonEverything.JsonSchema? schema;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once clean-up is complete.</returns>
    [GlobalSetup]
    public Task GlobalSetup()
    {
        this.objectDocument = JsonDocument.Parse(JsonText);

        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        for (int i = 0; i < 10000; ++i)
        {
            builder.Add(JsonAny.FromJson(this.objectDocument.RootElement));
        }

        this.personArrayV2 = Models.V2.PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();
        this.personArrayV3 = Models.V3.PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();

        this.schema = JsonEverything.JsonSchema.FromFile("./person-array-schema.json");
        this.node = System.Text.Json.Nodes.JsonArray.Create(this.personArrayV2.AsJsonElement.Clone());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Global clean-up.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once clean-up is complete.</returns>
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
    /// Validates using the V2 Corvus types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ValidateLargeArrayCorvusV2()
    {
        ValidationContext result = this.personArrayV2.Validate(ValidationContext.ValidContext, ValidationLevel.Basic);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the V3 Corvus types.
    /// </summary>
    [Benchmark]
    public void ValidateLargeArrayCorvusV3()
    {
        ValidationContext result = this.personArrayV3.Validate(ValidationContext.ValidContext, ValidationLevel.Basic);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark]
    public void ValidateLargeArrayJsonEverything()
    {
        JsonEverything.EvaluationResults result = this.schema!.Evaluate(this.node, Options);
    }
}