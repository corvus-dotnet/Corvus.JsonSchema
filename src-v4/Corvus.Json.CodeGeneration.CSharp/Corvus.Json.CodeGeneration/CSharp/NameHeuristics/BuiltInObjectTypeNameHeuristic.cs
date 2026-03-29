// <copyright file="BuiltInObjectTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in object type.
/// </summary>
public sealed class BuiltInObjectTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInObjectTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInObjectTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInObjectTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Object) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1)
        {
            // We are a simple string type
            foreach (IKeyword keyword in typeDeclaration.Keywords())
            {
                if (keyword is ICoreTypeValidationKeyword)
                {
                    continue;
                }

                if (keyword is INonStructuralKeyword)
                {
                    // Don't worry if it is a non-structural keyword.
                    continue;
                }

                if (keyword is IFallbackObjectPropertyTypeProviderKeyword)
                {
                    // Skip fallback object property type providers
                    // We are going to check for this later
                    continue;
                }

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            if (typeDeclaration.FallbackObjectPropertyType() is null || (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType fopt && fopt.ReducedType.IsBuiltInJsonAnyType()))
            {
                typeDeclaration.SetDotnetNamespace("Corvus.Json");
                typeDeclaration.SetDotnetTypeName("JsonObject");
                return true;
            }
        }

        return false;
    }
}