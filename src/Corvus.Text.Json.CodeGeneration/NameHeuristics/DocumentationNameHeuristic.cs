// <copyright file="DocumentationNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A name heuristic based on documentation keywords.
/// </summary>
public sealed class DocumentationNameHeuristic : INameHeuristicBeforeSubschema
{
    private DocumentationNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="DocumentationNameHeuristic"/>.
    /// </summary>
    public static DocumentationNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => true;

    /// <inheritdoc/>
    public uint Priority => 1_500;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = 0;
            return false;
        }

        if (typeDeclaration.ShortDocumentation() is string shortDocumentation &&
            shortDocumentation.Length > 0 && shortDocumentation.Length < 64)
        {
            written = Formatting.FormatTypeNameComponent(typeDeclaration, shortDocumentation.AsSpan(), typeNameBuffer);
            if (written > 1 && written < 64 && !typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
            {
                return true;
            }
        }

        if (typeDeclaration.LongDocumentation() is string longDocumentation &&
            longDocumentation.Length > 0 && longDocumentation.Length < 64)
        {
            written = Formatting.FormatTypeNameComponent(typeDeclaration, longDocumentation.AsSpan(), typeNameBuffer);
            if (written > 1 && written < 64 && !typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
            {
                return true;
            }
        }

        written = 0;
        return false;
    }
}