// <copyright file="EmitText.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Small shared helpers for emitting C# source text from the Arazzo workflow generators.
/// </summary>
internal static class EmitText
{
    /// <summary>
    /// Renders a string as a double-quoted C# string literal, escaping backslashes and quotes.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The C# string literal.</returns>
    public static string Quote(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <summary>
    /// Lower-cases the first character (PascalCase → camelCase) for local-variable names.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The camel-cased value.</returns>
    public static string ToCamelCase(string value)
        => value.Length == 0 || char.IsLower(value[0])
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Gets the name of the local that holds a step's built outputs object — the target of static
    /// <c>$steps.&lt;id&gt;.outputs</c> resolution.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The local name.</returns>
    public static string StepOutputsElementLocal(string stepId)
        => $"{ToCamelCase(SanitizeIdentifier(stepId))}OutputsElement";

    /// <summary>
    /// Produces a valid C# identifier from an arbitrary token (e.g. a step id), replacing characters
    /// that are not letters/digits with <c>_</c> and prefixing a leading digit.
    /// </summary>
    /// <param name="value">The token.</param>
    /// <returns>A valid C# identifier.</returns>
    public static string SanitizeIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return "_";
        }

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}