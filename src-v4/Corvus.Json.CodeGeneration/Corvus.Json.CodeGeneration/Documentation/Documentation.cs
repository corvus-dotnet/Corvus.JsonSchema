// <copyright file="Documentation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Documentation helpers.
/// </summary>
public static class Documentation
{
    /// <summary>
    /// Try to get short (summary-like) documentation for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to provide the short form documentation text.</param>
    /// <param name="shortDocumentation">The short-form documentation for the type declaration
    /// or <see langword="null"/> if no short form documentation was found.</param>
    /// <returns><see langword="true"/> if the short form documentation was found.</returns>
    /// <remarks>
    /// If multiple such keywords are present, the first alphabetically by keyword name will be used.
    /// </remarks>
    public static bool TryGetShortDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? shortDocumentation)
    {
        foreach (IShortDocumentationProviderKeyword keyword in typeDeclaration.Keywords().OfType<IShortDocumentationProviderKeyword>().OrderBy(k => k.Keyword))
        {
            if (keyword.TryGetShortDocumentation(typeDeclaration, out shortDocumentation))
            {
                return true;
            }
        }

        shortDocumentation = null;
        return false;
    }

    /// <summary>
    /// Try to get long (remarks-like) documentation for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to provide the long form documentation text.</param>
    /// <param name="longDocumentation">The long-form documentation for the type declaration
    /// or <see langword="null"/> if no short form documentation was found.</param>
    /// <returns><see langword="true"/> if the long form documentation was found.</returns>
    /// <remarks>
    /// Long form documentation is expected to be a multi-line string. If multiple long-form provider keywords are present,
    /// then the results will be appended in alphabetical order by keyword, to help with stability.
    /// </remarks>
    public static bool TryGetLongDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? longDocumentation)
    {
        StringBuilder builder = new();
        foreach (ILongDocumentationProviderKeyword keyword in typeDeclaration.Keywords().OfType<ILongDocumentationProviderKeyword>().OrderBy(k => k.Keyword))
        {
            if (keyword.TryGetLongDocumentation(typeDeclaration, out string? docs))
            {
                builder.AppendLine(docs);
            }
        }

        if (builder.Length > 0)
        {
            longDocumentation = builder.ToString();
            return true;
        }

        longDocumentation = null;
        return false;
    }

    /// <summary>
    /// Try to get examples for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to provide the examples text.</param>
    /// <param name="examples">The examples for the type declaration
    /// or <see langword="null"/> if no short form documentation was found.</param>
    /// <returns><see langword="true"/> if the long form documentation was found.</returns>
    /// <remarks>
    /// Examples documentation is expected to be a multi-line string. If multiple long-form provider keywords are present,
    /// then the results will be appended in alphabetical order by keyword, to help with stability.
    /// </remarks>
    public static bool TryGetExamples(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string[]? examples)
    {
        List<string> result = [];

        foreach (IExamplesProviderKeyword keyword in typeDeclaration.Keywords().OfType<IExamplesProviderKeyword>().OrderBy(k => k.Keyword))
        {
            if (keyword.TryGetExamples(typeDeclaration, out string[]? docs))
            {
                result.AddRange(docs);
            }
        }

        if (result.Count > 0)
        {
            examples = [.. result];
            return true;
        }

        examples = null;
        return false;
    }
}