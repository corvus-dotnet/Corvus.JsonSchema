// <copyright file="DocumentationNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

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
    public uint Priority => 5_000;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.ShortDocumentation() is string shortDocumentation &&
            shortDocumentation.Length > 0 && shortDocumentation.Length < 64)
        {
            written = Formatting.FormatTypeNameComponent(typeDeclaration, shortDocumentation.AsSpan(), typeNameBuffer);
            return true;
        }

        if (typeDeclaration.LongDocumentation() is string longDocumentation &&
            longDocumentation.Length > 0 && longDocumentation.Length < 64)
        {
            written = Formatting.FormatTypeNameComponent(typeDeclaration, longDocumentation.AsSpan(), typeNameBuffer);
            return true;
        }

        written = 0;
        return false;
    }
}