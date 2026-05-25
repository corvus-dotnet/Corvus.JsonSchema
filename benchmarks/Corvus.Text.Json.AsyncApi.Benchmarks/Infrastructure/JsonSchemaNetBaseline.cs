// <copyright file="JsonSchemaNetBaseline.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Json.Schema;

namespace AsyncApiBenchmark.Infrastructure;

/// <summary>
/// Wraps JsonSchema.Net evaluation for baseline comparison.
/// Pre-compiles the schema once during setup.
/// </summary>
public sealed class JsonSchemaNetBaseline
{
    private readonly JsonSchema schema;
    private readonly EvaluationOptions flagOptions;
    private readonly EvaluationOptions listOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaNetBaseline"/> class.
    /// </summary>
    /// <param name="schemaJson">The JSON Schema string.</param>
    public JsonSchemaNetBaseline(string schemaJson)
    {
        this.schema = JsonSchema.FromText(schemaJson);
        this.flagOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
        };
        this.listOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        };
    }

    /// <summary>
    /// Evaluates in flag mode (fastest interpreted validation — boolean result only).
    /// </summary>
    /// <param name="payloadJson">The payload bytes.</param>
    /// <returns><see langword="true"/> if valid.</returns>
    public bool EvaluateFlag(ReadOnlyMemory<byte> payloadJson)
    {
        using JsonDocument doc = JsonDocument.Parse(payloadJson);
        EvaluationResults result = this.schema.Evaluate(doc.RootElement, this.flagOptions);
        return result.IsValid;
    }

    /// <summary>
    /// Evaluates in list mode (full error collection — comparable to Corvus Detailed).
    /// </summary>
    /// <param name="payloadJson">The payload bytes.</param>
    /// <returns><see langword="true"/> if valid.</returns>
    public bool EvaluateList(ReadOnlyMemory<byte> payloadJson)
    {
        using JsonDocument doc = JsonDocument.Parse(payloadJson);
        EvaluationResults result = this.schema.Evaluate(doc.RootElement, this.listOptions);
        return result.IsValid;
    }
}