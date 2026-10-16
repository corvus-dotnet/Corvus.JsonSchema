// <copyright file="LanguageIds.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// The language identifiers the <c>--language</c> option accepts, and which of them have an emitter.
/// </summary>
public static class LanguageIds
{
    /// <summary>
    /// The C# emitter, the default.
    /// </summary>
    public const string CSharp = "csharp";

    /// <summary>
    /// The identifiers the option accepts, in the order the plan rolls them out.
    /// </summary>
    public static readonly string[] All = [CSharp, "go", "typescript", "java", "rust", "python", "julia", "ruby", "php", "r", "swift", "pascal"];

    /// <summary>
    /// The identifiers that currently have a client emitter.
    /// </summary>
    public static readonly string[] Supported = [CSharp];

    /// <summary>
    /// Normalises an option value to an identifier, or reports that it is not one.
    /// </summary>
    /// <param name="value">The option value, or <see langword="null"/> for the default.</param>
    /// <param name="languageId">The identifier.</param>
    /// <returns><see langword="true"/> when the value names a known language.</returns>
    public static bool TryNormalize(string? value, out string languageId)
    {
        if (string.IsNullOrEmpty(value))
        {
            languageId = CSharp;
            return true;
        }

        foreach (string candidate in All)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                languageId = candidate;
                return true;
            }
        }

        languageId = value!;
        return false;
    }
}