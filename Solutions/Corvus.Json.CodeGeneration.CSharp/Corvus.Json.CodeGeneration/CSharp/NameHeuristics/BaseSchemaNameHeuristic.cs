// <copyright file="BaseSchemaNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a base schema that will need to get its name from the path or reference.
/// </summary>
public sealed class BaseSchemaNameHeuristic : INameHeuristicBeforeSubschema
{
    private BaseSchemaNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BaseSchemaNameHeuristic"/>.
    /// </summary>
    public static BaseSchemaNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1000;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = GetCandidateNameFromReference(typeDeclaration, reference, typeNameBuffer);
            if (written == 0)
            {
                return false;
            }

            written = Formatting.FormatTypeNameComponent(typeDeclaration, typeNameBuffer[..written], typeNameBuffer);

            if (!typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
            {
                return true;
            }

            written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
            return true;
        }

        written = 0;
        return false;
    }

    private static int GetCandidateNameFromReference(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer)
    {
        if (reference.HasFragment)
        {
            int lastSlash = reference.Fragment.LastIndexOf('/');
            ReadOnlySpan<char> lastSegment = reference.Fragment[(lastSlash + 1)..];
            return Formatting.FormatTypeNameComponent(typeDeclaration, lastSegment, typeNameBuffer);
        }
        else if (reference.HasPath)
        {
            return GetNameFromPath(typeDeclaration, reference, typeNameBuffer);
        }

        return 0;
    }

    private static int GetNameFromPath(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer)
    {
        if (typeDeclaration.Parent()?.DotnetTypeName() is string name)
        {
            name.AsSpan().CopyTo(typeNameBuffer);
            return name.Length;
        }

        int lastSlash = reference.Path.LastIndexOf('/');
        if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
        {
            lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
            return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..], typeNameBuffer);
        }
        else if (lastSlash == reference.Path.Length - 1)
        {
            return 0;
        }

        int lastDot = reference.Path.LastIndexOf('.');
        if (lastDot > 0 && lastSlash < lastDot)
        {
            return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..lastDot], typeNameBuffer);
        }

        return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..], typeNameBuffer);
    }
}