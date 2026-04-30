// <copyright file="SubschemaNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on an inline subschema.
/// </summary>
public sealed class SubschemaNameHeuristic : INameHeuristicBeforeSubschema
{
    private SubschemaNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="SubschemaNameHeuristic"/>.
    /// </summary>
    public static SubschemaNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 10_000;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent && !typeDeclaration.IsInDefinitionsContainer())
        {
            if (reference.HasFragment)
            {
                int lastSlash = reference.Fragment.LastIndexOf('/');
                if (lastSlash > 0 && lastSlash < reference.Fragment.Length - 1 && char.IsDigit(reference.Fragment[lastSlash + 1]))
                {
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');
                    if (previousSlash >= 0)
                    {
                        lastSlash = previousSlash;
                    }

                    ReadOnlySpan<char> name = reference.Fragment[(lastSlash + 1)..];
                    written = Formatting.FormatTypeNameComponent(typeDeclaration, name, typeNameBuffer);
                }
                else if ((parent.AllowedCoreTypes() & CoreTypes.Array) != 0 && lastSlash > 0 && !typeDeclaration.IsInDefinitionsContainer())
                {
                    // If this is an inline definition in an array, we will we build it from the keyword name.
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');

                    ReadOnlySpan<char> name = reference.Fragment[(previousSlash + 1)..lastSlash];
                    written = Formatting.FormatTypeNameComponent(typeDeclaration, name, typeNameBuffer);
                }
                else
                {
                    ReadOnlySpan<char> name = reference.Fragment[(lastSlash + 1)..];
                    written = Formatting.FormatTypeNameComponent(typeDeclaration, name, typeNameBuffer);
                }

                written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);

                return true;
            }
        }

        written = 0;
        return false;
    }
}