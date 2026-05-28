// <copyright file="SchemaValidationDetail.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Formats <see cref="JsonSchemaResultsCollector"/> results as a JSON
/// string suitable for inclusion in exception messages.
/// </summary>
public static class SchemaValidationDetail
{
    /// <summary>
    /// Formats the validation results from a <see cref="JsonSchemaResultsCollector"/>
    /// as a JSON array string.
    /// </summary>
    /// <param name="collector">The results collector containing validation output.</param>
    /// <returns>
    /// A JSON-formatted string containing an array of error objects, each with
    /// <c>valid</c>, <c>evaluationPath</c>, <c>schemaLocation</c>,
    /// <c>instanceLocation</c>, and <c>message</c> properties.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is intended for diagnostic output only. The format is informational
    /// and may change between versions.
    /// </para>
    /// </remarks>
    public static string FormatResults(JsonSchemaResultsCollector collector)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ValidationDetail.Mutable> builder =
            ValidationDetail.CreateBuilder(workspace);

        ValidationDetail.Mutable root = builder.RootElement;

        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            string message = result.GetMessageText();
            string evaluationPath = result.GetEvaluationLocationText();
            string instanceLocation = result.GetDocumentEvaluationLocationText();
            string schemaLocation = result.GetSchemaEvaluationLocationText();
            bool isMatch = result.IsMatch;

            root.AddItem(
                new ValidationDetail.ValidationDetailItem.Source(
                    (ref ValidationDetail.ValidationDetailItem.Builder b) =>
                        b.Create(
                            evaluationPath,
                            instanceLocation,
                            schemaLocation,
                            isMatch,
                            message.Length > 0 ? (Internal.JsonString.Source)message : default)));
        }

        return root.ToString();
    }
}