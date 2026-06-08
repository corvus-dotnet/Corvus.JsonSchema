// <copyright file="EmitText.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Small shared helpers for emitting C# source text from the Arazzo workflow generators.
/// </summary>
internal static class EmitText
{
    /// <summary>
    /// Renders a string as a double-quoted C# string literal, escaping backslashes, quotes, and all
    /// control characters (so arbitrary text — including JSON with newlines — embeds safely).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The C# string literal.</returns>
    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\0': builder.Append("\\0"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

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
    /// Produces a PascalCase C# identifier from an arbitrary token, treating any run of non-alphanumeric
    /// characters as a word boundary (e.g. <c>adopt-pet</c> → <c>AdoptPet</c>) — used for generated
    /// type names such as the per-workflow executor class.
    /// </summary>
    /// <param name="value">The token.</param>
    /// <returns>A PascalCase identifier.</returns>
    public static string ToPascalCase(string value)
    {
        var builder = new StringBuilder(value.Length);
        bool upperNext = true;
        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c))
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        if (builder.Length == 0)
        {
            return "_";
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

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