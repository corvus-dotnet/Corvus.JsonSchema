// <copyright file="BuiltInArrayTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in array type.
/// </summary>
public sealed class BuiltInArrayTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInArrayTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInArrayTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInArrayTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Array) != 0 &&
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

                if (keyword is IArrayItemsTypeProviderKeyword)
                {
                    // Skip non-tuple array items providers
                    // We are going to check for this later
                    continue;
                }

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration aitd && aitd.ReducedType.IsJsonAnyType())
            {
                typeDeclaration.SetDotnetNamespace("Corvus.Json");
                typeDeclaration.SetDotnetTypeName("JsonArray");
                return true;
            }
        }

        return false;
    }
}