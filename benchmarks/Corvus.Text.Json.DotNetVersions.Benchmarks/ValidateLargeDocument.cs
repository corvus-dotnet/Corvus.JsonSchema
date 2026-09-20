// <copyright file="ValidateLargeDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.ClassicBenchmarkModels;
using Corvus.Text.Json;
using Corvus.Text.Json.Validator;

namespace Corvus.Text.Json.DotNetVersions.Benchmarks;

/// <summary>
/// Validates an array of 10,000 small documents with each of the V5 validation paths.
/// </summary>
/// <remarks>
/// This is the same document and schema as the V4 <c>ValidateLargeArrayCorvusV4</c> benchmark.
/// </remarks>
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

    private ParsedJsonDocument<PersonArray>? document;
    private ParsedJsonDocument<JsonElement>? elementDocument;
    private JsonSchema dynamicSchema;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        StringBuilder builder = new("[");
        for (int i = 0; i < 10000; ++i)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(JsonText);
        }

        builder.Append(']');
        string json = builder.ToString();

        this.document = ParsedJsonDocument<PersonArray>.Parse(json);
        this.elementDocument = ParsedJsonDocument<JsonElement>.Parse(json);
        this.dynamicSchema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "person-array-schema.json"));
    }

    /// <summary>
    /// Global clean-up.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.document?.Dispose();
        this.elementDocument?.Dispose();
    }

    /// <summary>
    /// Validates using the V5 generated types.
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusV5GeneratedTypes()
    {
        return this.document!.RootElement.EvaluateSchema();
    }

    /// <summary>
    /// Validates using the V5 generated standalone evaluator.
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusV5StandaloneEvaluator()
    {
        PersonArray root = this.document!.RootElement;
        return PersonArrayEvaluator.Evaluate(in root);
    }

    /// <summary>
    /// Validates using the V5 dynamic validator (a schema loaded at runtime).
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusV5DynamicValidator()
    {
        return this.dynamicSchema.Validate(this.elementDocument!.RootElement);
    }
}