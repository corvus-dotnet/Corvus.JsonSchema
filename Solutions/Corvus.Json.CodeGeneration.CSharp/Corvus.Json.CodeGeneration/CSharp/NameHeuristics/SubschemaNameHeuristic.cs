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
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent)
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
                    name.CopyTo(typeNameBuffer);
                    written = name.Length;
                    written = Formatting.ToPascalCase(typeNameBuffer[..written]);
                }
                else if ((parent.AllowedCoreTypes() & CoreTypes.Array) != 0)
                {
                    // If this is an inline definition in an array, we will we build it from the keyword name.
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');

                    ReadOnlySpan<char> name = reference.Fragment[(previousSlash + 1)..lastSlash];
                    name.CopyTo(typeNameBuffer);
                    written = name.Length;
                    written = Formatting.ToPascalCase(typeNameBuffer[..written]);
                }
                else
                {
                    ReadOnlySpan<char> name = reference.Fragment[(lastSlash + 1)..];
                    name.CopyTo(typeNameBuffer);
                    written = name.Length;
                    written = Formatting.ToPascalCase(typeNameBuffer[..written]);
                }

                if (CollidesWithParent(typeDeclaration, typeNameBuffer[..written]) ||
                    MatchesExistingPropertyNameInParent(typeDeclaration, typeNameBuffer[..written]))
                {
                    written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
                }

                int index = 1;
                int writtenBefore = written;

                while (MatchesExistingTypeInParent(typeDeclaration, typeNameBuffer[..written]) ||
                       MatchesExistingPropertyNameInParent(typeDeclaration, typeNameBuffer[..written]))
                {
                    written = writtenBefore + Formatting.ApplySuffix(index, typeNameBuffer[..writtenBefore]);
                    index++;
                }

                return true;
            }
        }

        written = 0;
        return false;
    }

    private static bool MatchesExistingPropertyNameInParent(TypeDeclaration typeDeclaration, ReadOnlySpan<char> corvusTypeNameBuffer)
    {
        TypeDeclaration? parent = typeDeclaration.Parent();

        if (parent is null)
        {
            return false;
        }

        foreach (PropertyDeclaration propertyDeclaration in parent.PropertyDeclarations)
        {
            if (propertyDeclaration.DotnetPropertyName().AsSpan().Equals(corvusTypeNameBuffer, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CollidesWithParent(TypeDeclaration typeDeclaration, ReadOnlySpan<char> corvusTypeNameBuffer)
    {
        return
            typeDeclaration.Parent() is TypeDeclaration parent &&
            corvusTypeNameBuffer.Equals(parent.DotnetTypeName().AsSpan(), StringComparison.Ordinal);
    }

    private static bool MatchesExistingTypeInParent(TypeDeclaration typeDeclaration, ReadOnlySpan<char> corvusTypeNameBuffer)
    {
        TypeDeclaration? parent = typeDeclaration.Parent();

        if (parent is null)
        {
            return false;
        }

        foreach (TypeDeclaration child in parent.Children())
        {
            if (child.TryGetDotnetTypeName(out string? name) &&
                 corvusTypeNameBuffer.Equals(name.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}