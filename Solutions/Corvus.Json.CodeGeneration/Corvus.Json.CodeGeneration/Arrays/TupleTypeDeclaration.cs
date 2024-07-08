// <copyright file="TupleTypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Describes the type of the items in an array.
/// </summary>
/// <param name="itemsTypes">The types of the items in the array.</param>
/// <param name="isExplicit"><see langword="true"/> if the tuple type is explicitly
/// defined on the type declaration.</param>
public sealed class TupleTypeDeclaration(TypeDeclaration[] itemsTypes, bool isExplicit)
{
    /// <summary>
    /// Gets a value indicating whether the array items type
    /// is explicitly defined on the
    /// type declaration.
    /// </summary>
    public bool IsExplicit { get; } = isExplicit;

    /// <summary>
    /// Gets the type of the items in the array.
    /// </summary>
    public ReducedTypeDeclaration[] ItemsTypes { get; } = itemsTypes.Select(i => i.ReducedTypeDeclaration()).ToArray();

    /// <summary>
    /// Gets the unreduced type of the items in the array.
    /// </summary>
    public TypeDeclaration[] UnreducedTypes { get; } = itemsTypes;

    /// <summary>
    /// Determines whether this is an equivalent type to the
    /// target type.
    /// </summary>
    /// <param name="other">The other <see cref="TupleTypeDeclaration"/>.</param>
    /// <returns><see langword="true"/> if the reduced items types for both declarations are
    /// the same, in order.</returns>
    public bool ItemsTypesSequenceEquals(TupleTypeDeclaration other)
    {
        if (other.ItemsTypes.Length != this.ItemsTypes.Length)
        {
            return false;
        }

        for (int i = 0; i < this.ItemsTypes.Length; ++i)
        {
            if (this.ItemsTypes[i].ReducedType != other.ItemsTypes[i].ReducedType)
            {
                return false;
            }
        }

        return true;
    }
}