// <copyright file="ValidateLargeDocument.cs" company="Endjin Limited">
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
    private Models.V3.PersonArray personArrayV3;
    private Models.V4.PersonArray personArrayV4;
    private JsonNode? node;
    private JsonEverything.JsonSchema? schema;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.objectDocument = JsonDocument.Parse(JsonText);

        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        for (int i = 0; i < 10000; ++i)
        {
            builder.Add(Models.V3.Person.FromJson(this.objectDocument.RootElement));
        }

        this.personArrayV3 = Models.V3.PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();
        this.personArrayV4 = Models.V4.PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();

        this.schema = JsonEverything.JsonSchema.FromFile("./person-array-schema.json");
        this.node = System.Text.Json.Nodes.JsonArray.Create(this.personArrayV3.AsJsonElement.Clone());
    }

    /// <summary>
    /// Global clean-up.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (this.objectDocument is JsonDocument doc)
        {
            doc.Dispose();
        }
    }

    /// <summary>
    /// Validates using the Corvus V3 types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool ValidateLargeArrayCorvusV3()
    {
        ValidationContext result = this.personArrayV3.Validate(ValidationContext.ValidContext);
        return result.IsValid;
    }

    /// <summary>
    /// Validates using the Corvus V4 types.
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusV4()
    {
        ValidationContext result = this.personArrayV4.Validate(ValidationContext.ValidContext);
        return result.IsValid;
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark]
    public JsonEverything.EvaluationResults ValidateLargeArrayJsonEverything()
    {
         return this.schema!.Evaluate(this.node, Options);
    }
}