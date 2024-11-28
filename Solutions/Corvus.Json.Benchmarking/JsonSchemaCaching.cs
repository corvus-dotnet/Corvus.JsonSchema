// <copyright file="JsonSchemaCaching.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using CorvusValidator = global::Corvus.Json.Validator;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class JsonSchemaCaching
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

    private JsonDocument? objectDocument;
    private JsonElement element;
    private CorvusValidator.JsonSchema corvusSchema;

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

        Models.V3.PersonArray personArrayV3 = Models.V3.PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();
        this.element = personArrayV3.AsJsonElement.Clone();
        this.corvusSchema = CorvusValidator.JsonSchema.FromFile("./person-array-schema.json");
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
    /// Validates using the Corvus V4 types.
    /// </summary>
    [Benchmark]
    public bool ValidateLargeArrayCorvusValidator()
    {
        ValidationContext result = this.corvusSchema.Validate(this.element, ValidationLevel.Flag);
        return result.IsValid;
    }
}