// <copyright file="Composition.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for composition.
/// </summary>
public static class Composition
{
    /// <summary>
    /// Gets a value indicating that the given type declaration has a single implied <see cref="CoreTypes"/> value.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if this implies a single core type.</returns>
    public static bool IsSingleCoreType(TypeDeclaration typeDeclaration) => typeDeclaration.ImpliedCoreTypes().CountTypes() == 1;

    /// <summary>
    /// Gets the implied core type for subschema.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration whose core type is to be determined.</param>
    /// <param name="currentCoreTypes">The current implied core types.</param>
    /// <returns>The composed core types.</returns>
    public static CoreTypes UnionImpliesCoreTypeForTypeDeclaration(TypeDeclaration typeDeclaration, in CoreTypes currentCoreTypes)
    {
        CoreTypes result = currentCoreTypes;
        foreach (IKeyword keyword in typeDeclaration.Keywords())
        {
            result |= keyword.ImpliesCoreTypes(typeDeclaration);
        }

        return result;
    }

    /// <summary>
    /// Gets the implied core type for subschema.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration whose core type is to be determined.</param>
    /// <param name="keywordPath">The keyword path.</param>
    /// <param name="currentCoreTypes">The current implied core types.</param>
    /// <returns>The composed core types.</returns>
    public static CoreTypes UnionImpliesCoreTypeForSubschema(TypeDeclaration typeDeclaration, string keywordPath, in CoreTypes currentCoreTypes)
    {
        CoreTypes result = currentCoreTypes;

        foreach (TypeDeclaration subschemaTypeDeclaration in typeDeclaration.SubschemaTypeDeclarations.Where(k => k.Key.StartsWith(keywordPath)).Select(kvp => kvp.Value))
        {
            result |= subschemaTypeDeclaration.ReducedTypeDeclaration().ReducedType.ImpliedCoreTypes();
        }

        return result;
    }
}