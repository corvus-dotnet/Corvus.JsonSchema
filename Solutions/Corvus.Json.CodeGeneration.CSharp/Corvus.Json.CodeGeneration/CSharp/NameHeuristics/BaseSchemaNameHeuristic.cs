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
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = GetCandidateNameFromReference(typeDeclaration, reference, typeNameBuffer);
            if (written == 0)
            {
                return false;
            }

            ReadOnlySpan<char> name = typeNameBuffer[..written];
            bool collidesWithParent = typeDeclaration.CollidesWithParent(name);
            if (!collidesWithParent && typeDeclaration.MatchesExistingTypeInParent(name))
            {
                if (reference.HasPath && reference.HasFragment)
                {
                    // Secondary buffer for name composition. Its contents are always disposable
                    Span<char> interimBuffer = stackalloc char[Formatting.MaxIdentifierLength];
                    int pathNameLength = GetNameFromPath(typeDeclaration, reference, interimBuffer);
                    int formatted = Formatting.FormatCompositeName(typeDeclaration, typeNameBuffer, interimBuffer[..pathNameLength], name);
                    name = typeNameBuffer[..formatted];
                }
            }

            written = Formatting.FormatTypeNameComponent(typeDeclaration, name, typeNameBuffer);

            if (collidesWithParent)
            {
                written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
            }

            int index = 1;
            int writtenBefore = written;

            while (typeDeclaration.MatchesExistingTypeInParent(typeNameBuffer[..written]) ||
                   typeDeclaration.MatchesExistingPropertyNameInParent(typeNameBuffer[..written]))
            {
                written = writtenBefore + Formatting.ApplySuffix(index, typeNameBuffer[writtenBefore..]);
                index++;
            }

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