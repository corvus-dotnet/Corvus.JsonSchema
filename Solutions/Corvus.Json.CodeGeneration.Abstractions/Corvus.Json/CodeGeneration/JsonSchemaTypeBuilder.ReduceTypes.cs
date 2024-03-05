// <copyright file="JsonSchemaTypeBuilder.ReduceTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Walks a JSON schema and builds a type map of it.
/// </summary>
public partial class JsonSchemaTypeBuilder
{
    /// <summary>
    /// Gets the reduced type declaration for the specified location.
    /// </summary>
    /// <param name="location">The location for which to get the type declaration.</param>
    /// <param name="typeDeclaration">The reduced type declaraiton for the specified location.</param>
    /// <returns><see langword="true"/> if a type declaration was found for the location.</returns>
    internal bool TryGetReducedTypeDeclarationFor(JsonReference location, [NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
    {
        if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? baseTypeDeclaration))
        {
            if (baseTypeDeclaration.TryGetReducedType(out TypeDeclaration reducedType))
            {
                typeDeclaration = reducedType;
                return true;
            }

            typeDeclaration = baseTypeDeclaration;
            return true;
        }

        typeDeclaration = null;
        return false;
    }

    /// <summary>
    /// This reduces the type declarations required by the root type declaration,
    /// including the root type declaration itself.
    /// </summary>
    private TypeDeclaration ReduceTypeDeclarations(TypeDeclaration root)
    {
        Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache = [];
        if (this.TryReduceTypeDeclarationsCore(root, reductionCache, out TypeDeclaration? reducedType))
        {
            if (root.LocatedSchema.Location != reducedType.LocatedSchema.Location)
            {
                this.locatedTypeDeclarations.Remove(root.LocatedSchema.Location);
                this.locatedTypeDeclarations.Add(root.LocatedSchema.Location, reducedType);
            }

            return reducedType;
        }

        return root;
    }

    private bool TryReduceTypeDeclarationsCore(TypeDeclaration typeDeclaration, Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache, [NotNullWhen(true)] out TypeDeclaration? reducedType)
    {
        if (reductionCache.TryGetValue(typeDeclaration, out TypeDeclaration? cachedReduction))
        {
            reducedType = cachedReduction;

            // Null means we didn't reduce it.
            return cachedReduction is not null;
        }

        typeDeclaration.TryGetReducedType(out reducedType);

        // Add the mapping (which will be to a null instance if the item wasn't reduced;
        reductionCache.Add(typeDeclaration, reducedType);
        this.ReducedRefResolvableProperties(reducedType ?? typeDeclaration, reductionCache);
        return reducedType is not null;
    }

    private void ReducedRefResolvableProperties(TypeDeclaration typeDeclaration, Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache)
    {
        foreach (KeyValuePair<string, TypeDeclaration> refResolvablePropertyDeclaration in typeDeclaration.RefResolvablePropertyDeclarations.ToList())
        {
            if (this.TryReduceTypeDeclarationsCore(refResolvablePropertyDeclaration.Value, reductionCache, out TypeDeclaration? reducedType))
            {
                typeDeclaration.ReplaceRefResolvablePropertyDeclaration(refResolvablePropertyDeclaration.Key, reducedType);
            }
        }
    }
}