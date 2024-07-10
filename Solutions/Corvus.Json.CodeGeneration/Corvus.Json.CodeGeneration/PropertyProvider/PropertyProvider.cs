// <copyright file="PropertyProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for property providers.
/// </summary>
public static class PropertyProvider
{
    /// <summary>
    /// Collects the properties for a source type declaration, adding them to a target type.
    /// </summary>
    /// <param name="source">The source type producing the properties.</param>
    /// <param name="target">The target type to which properties are to be added.</param>
    /// <param name="visitedTypeDeclarations">The types we have already visited.</param>
    /// <param name="treatRequiredAsOptional">Whether required properties are to be treated as optional.</param>
    /// <returns><see langword="true"/> if properties were collected from this type.</returns>
    public static bool CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (visitedTypeDeclarations.Contains(source))
        {
            return false;
        }

        visitedTypeDeclarations.Add(source);

        HashSet<TypeDeclaration> childContext = [];

        // We don't need to check for keyword hiding here; this will be dealt with in the CollectProperties() implementations
        // Typically, this is achieved by using the subschema to determine the properties to add. The subschema will not have
        // been added for hidden siblings.
        foreach (IPropertyProviderKeyword? propertyProviderKeyword in source.Keywords().OfType<IPropertyProviderKeyword>().OrderBy(k => k.PropertyProviderPriority))
        {
            propertyProviderKeyword.CollectProperties(source, target, childContext, treatRequiredAsOptional);
        }

        return true;
    }
}