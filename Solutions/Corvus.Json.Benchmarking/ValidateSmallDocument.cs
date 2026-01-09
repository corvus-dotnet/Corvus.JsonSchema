// <copyright file="ValidateSmallDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using CorvusValidator = global::Corvus.Json.Validator;
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
    private Models.V3.Person personV3;
    private Models.V4.Person personV4;
    private JsonElement element;
    private JsonEverything.JsonSchema? schema;
    private CorvusValidator.JsonSchema corvusSchema;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.objectDocument = JsonDocument.Parse(JsonText);
        this.personV3 = Models.V3.Person.FromJson(this.objectDocument.RootElement.Clone());
        this.personV4 = Models.V4.Person.FromJson(this.objectDocument.RootElement.Clone());
        this.schema = JsonEverything.JsonSchema.FromFile("./person-schema.json");
        this.corvusSchema = CorvusValidator.JsonSchema.FromFile("./person-schema.json");
        this.element = this.personV3.AsJsonElement.Clone();
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
    public bool ValidateSmallDocumentCorvusV3()
    {
        ValidationContext result = this.personV3.Validate(ValidationContext.ValidContext);
        return result.IsValid;
    }

    /// <summary>
    /// Validates using the Corvus V3 types.
    /// </summary>
    [Benchmark]
    public bool ValidateSmallDocumentCorvusV4()
    {
        ValidationContext result = this.personV4.Validate(ValidationContext.ValidContext);
        return result.IsValid;
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark]
    public JsonEverything.EvaluationResults ValidateSmallDocumentJsonEverything()
    {
        return this.schema!.Evaluate(this.element, Options);
    }

    /// <summary>
    /// Validates using the Corvus Validator.
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusValidator()
    {
        ValidationContext result = this.corvusSchema.Validate(this.element, ValidationLevel.Flag);
        return result.IsValid;
    }
}