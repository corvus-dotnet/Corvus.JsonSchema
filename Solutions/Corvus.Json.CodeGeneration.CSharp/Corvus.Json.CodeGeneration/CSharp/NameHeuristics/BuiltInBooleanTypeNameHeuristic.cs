// <copyright file="BuiltInBooleanTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in boolean type.
/// </summary>
public sealed class BuiltInBooleanTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInBooleanTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInBooleanTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInBooleanTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Boolean) != 0 &&
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

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            typeDeclaration.SetDotnetNamespace("Corvus.Json");
            typeDeclaration.SetDotnetTypeName("JsonBoolean");
            return true;
        }

        return false;
    }
}