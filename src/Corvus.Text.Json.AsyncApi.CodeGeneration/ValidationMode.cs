// <copyright file="ValidationMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Controls the level of JSON Schema validation applied to message payloads
/// and headers in producers and consumers.
/// </summary>
public enum ValidationMode
{
    /// <summary>
    /// No validation is performed. Use when you trust the inputs or have
    /// already validated externally.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validation uses a fast boolean check via
    /// <c>EvaluateSchema()</c>. On failure, the exception
    /// message identifies the failing message but does not include
    /// detailed schema diagnostics.
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Validation uses a <see cref="Corvus.Text.Json.JsonSchemaResultsCollector"/>
    /// to capture detailed evaluation results. On failure, the exception
    /// message includes a JSON-formatted array of validation errors with
    /// evaluation paths, schema locations, and error messages.
    /// </summary>
    Detailed = 2,
}