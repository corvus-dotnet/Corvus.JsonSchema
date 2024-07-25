// <copyright file="ValidateSmallDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using JsonEverything = global::Json.Schema;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class ValidateSmallDocument
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
    private Models.V2.Person personV2;
    private Models.V3.Person personV3;
    private JsonNode? node;
    private JsonEverything.JsonSchema? schema;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.objectDocument = JsonDocument.Parse(JsonText);
        this.personV2 = Models.V2.Person.FromJson(this.objectDocument.RootElement.Clone());
        this.personV3 = Models.V3.Person.FromJson(this.objectDocument.RootElement.Clone());
        this.schema = JsonEverything.JsonSchema.FromFile("./person-schema.json");
        this.node = System.Text.Json.Nodes.JsonObject.Create(this.personV2.AsJsonElement.Clone());
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
    /// Validates using the Corvus V2 types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ValidateSmallDocumentCorvusV2()
    {
        ValidationContext result = this.personV2.Validate(ValidationContext.ValidContext);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the Corvus V2 types.
    /// </summary>
    [Benchmark]
    public void ValidateSmallDocumentCorvusV3()
    {
        ValidationContext result = this.personV3.Validate(ValidationContext.ValidContext);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark]
    public void ValidateSmallDocumentJsonEverything()
    {
        JsonEverything.EvaluationResults result = this.schema!.Evaluate(this.node, Options);
    }
}