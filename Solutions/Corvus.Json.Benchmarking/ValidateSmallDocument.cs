﻿// <copyright file="ValidateSmallDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    private Person person;
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
        this.person = Person.FromJson(this.objectDocument.RootElement);
        this.schema = JsonEverything.JsonSchema.FromFile("./PersonModel/person-schema.json");
        this.node = System.Text.Json.Nodes.JsonObject.Create(this.person.AsJsonElement.Clone());
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
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark(Baseline = true)]
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
    [Benchmark]
    public void ValidateSmallDocumentJsonEverything()
    {
        JsonEverything.EvaluationResults result = this.schema!.Evaluate(this.node, Options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException();
        }
    }
}