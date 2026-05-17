// <copyright file="SchemaValidationDetail.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

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
        StringBuilder sb = new();
        sb.Append('[');

        bool first = true;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(',');
            }

            sb.Append("{\"valid\":");
            sb.Append(result.IsMatch ? "true" : "false");

            sb.Append(",\"evaluationPath\":\"");
            AppendJsonEscaped(sb, result.GetEvaluationLocationText());

            sb.Append("\",\"schemaLocation\":\"");
            AppendJsonEscaped(sb, result.GetSchemaEvaluationLocationText());

            sb.Append("\",\"instanceLocation\":\"");
            AppendJsonEscaped(sb, result.GetDocumentEvaluationLocationText());

            string message = result.GetMessageText();
            if (message.Length > 0)
            {
                sb.Append("\",\"message\":\"");
                AppendJsonEscaped(sb, message);
            }

            sb.Append("\"}");
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static void AppendJsonEscaped(StringBuilder sb, string value)
    {
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
    }
}