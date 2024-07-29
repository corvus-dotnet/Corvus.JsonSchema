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
    /// Collects the properties for a source type declaration, and keyword path, adding them to a target type.
    /// </summary>
    /// <param name="keywordPath">The keyword path to the subschema. The final component of the path is the name of the property defined.</param>
    /// <param name="source">The source type producing the properties.</param>
    /// <param name="target">The target type to which properties are to be added.</param>
    /// <param name="visitedTypeDeclarations">The types we have already visited.</param>
    /// <param name="treatRequiredAsOptional">Whether required properties are to be treated as optional.</param>
    /// <returns><see langword="true"/> if properties were collected from this type.</returns>
    public static bool CollectPropertiesForMapOfPropertyNameToSchema(
        string keywordPath,
        TypeDeclaration source,
        TypeDeclaration target,
        HashSet<TypeDeclaration> visitedTypeDeclarations,
        bool treatRequiredAsOptional)
    {
        foreach (KeyValuePair<string, TypeDeclaration> subschema in
                    source.SubschemaTypeDeclarations
                    .Where(kvp => kvp.Key.StartsWith(keywordPath)))
        {
            target.AddOrUpdatePropertyDeclaration(
                new(
                    target,
                    GetPropertyName(subschema.Key),
                    WellKnownTypeDeclarations.JsonAny,
                    RequiredOrOptional.Optional,
                    LocalOrComposed.Composed,
                    null,
                    null));

            PropertyProvider.CollectProperties(
                subschema.Value,
                target,
                visitedTypeDeclarations,
                treatRequiredAsOptional);
        }

        return true;

        static string GetPropertyName(string key)
        {
            return key[(key.LastIndexOf('/') + 1)..];
        }
    }

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