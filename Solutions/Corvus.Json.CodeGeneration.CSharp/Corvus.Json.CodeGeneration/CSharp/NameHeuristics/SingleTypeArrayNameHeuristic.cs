// <copyright file="SingleTypeArrayNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on single-type arrays.
/// </summary>
public sealed class SingleTypeArrayNameHeuristic : INameHeuristicAfterSubschema
{
    private SingleTypeArrayNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="SingleTypeArrayNameHeuristic"/>.
    /// </summary>
    public static SingleTypeArrayNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => true;

    /// <inheritdoc/>
    public uint Priority => 1600;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent &&
            !typeDeclaration.IsInDefinitionsContainer() &&
            typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsTypeDeclaration &&
            itemsTypeDeclaration.IsExplicit &&
            typeDeclaration.ImpliedCoreTypes().CountTypes() == 1)
        {
            ReadOnlySpan<char> itemsNameSpan = itemsTypeDeclaration.ReducedType.DotnetTypeName().AsSpan();
            itemsNameSpan.CopyTo(typeNameBuffer);
            written = itemsNameSpan.Length;
            int arraySuffixLength = Formatting.ApplyArraySuffix(typeNameBuffer[written..]);
            written += arraySuffixLength;

            foreach (TypeDeclaration child in parent.Children())
            {
                if (child.ReducedTypeDeclaration().ReducedType.DotnetTypeName().AsSpan().Equals(typeNameBuffer[..written], StringComparison.Ordinal))
                {
                    written = PrependTypeDeclarationName(typeDeclaration, arraySuffixLength, typeNameBuffer, itemsNameSpan);
                    break;
                }
            }

            if (parent.DotnetTypeName().AsSpan().Equals(typeNameBuffer[..written], StringComparison.Ordinal))
            {
                written = PrependTypeDeclarationName(typeDeclaration, arraySuffixLength, typeNameBuffer, itemsNameSpan);
            }

            return true;
        }

        written = 0;
        return false;

        static int PrependTypeDeclarationName(TypeDeclaration typeDeclaration, int arraySuffixLength, Span<char> typeNameBuffer, ReadOnlySpan<char> itemsNameSpan)
        {
            ReadOnlySpan<char> nameWithoutArraySuffix = typeDeclaration.DotnetTypeName().AsSpan()[..^arraySuffixLength];

            nameWithoutArraySuffix.CopyTo(typeNameBuffer);
            int written = nameWithoutArraySuffix.Length;
            itemsNameSpan.CopyTo(typeNameBuffer[written..]);
            written = itemsNameSpan.Length;
            written += Formatting.ApplyArraySuffix(typeNameBuffer[written..]);
            return written;
        }
    }
}