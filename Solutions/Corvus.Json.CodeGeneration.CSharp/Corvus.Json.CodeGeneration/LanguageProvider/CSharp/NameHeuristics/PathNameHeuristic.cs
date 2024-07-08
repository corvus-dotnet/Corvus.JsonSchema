// <copyright file="PathNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a path index.
/// </summary>
public sealed class PathNameHeuristic : INameHeuristicBeforeSubschema
{
    private PathNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="PathNameHeuristic"/>.
    /// </summary>
    public static PathNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 11_000;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is not null &&
            reference.HasPath)
        {
            int lastSlash = reference.Path.LastIndexOf('/');
            if (lastSlash == reference.Path.Length - 1)
            {
                lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
            }

            if (char.IsDigit(reference.Path[lastSlash + 1]))
            {
                int previousSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                if (previousSlash >= 0)
                {
                    lastSlash = previousSlash;
                }
            }

            ReadOnlySpan<char> name = reference.Path[(lastSlash + 1)..];
            name.CopyTo(typeNameBuffer);
            written = name.Length;
            written = Formatting.ToPascalCase(typeNameBuffer[..written]);
            written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
            return true;
        }

        written = 0;
        return false;
    }
}