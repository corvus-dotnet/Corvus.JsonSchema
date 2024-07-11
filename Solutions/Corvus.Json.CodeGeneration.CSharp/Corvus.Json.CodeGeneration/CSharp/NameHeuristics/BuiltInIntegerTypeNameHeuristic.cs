// <copyright file="BuiltInIntegerTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in number type.
/// </summary>
public sealed class BuiltInIntegerTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInIntegerTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInIntegerTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInIntegerTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Integer) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1)
        {
            string? candidateFormat = null;

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

                if (keyword is IFormatValidationKeyword formatKeyword)
                {
                    formatKeyword.TryGetFormat(typeDeclaration, out candidateFormat);
                    continue;
                }

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            typeDeclaration.SetDotnetNamespace("Corvus.Json");

            if (candidateFormat is string format)
            {
                typeDeclaration.SetDotnetTypeName(
                    WellKnownNumericFormatHelpers.GetIntegerDotnetTypeNameFor(format));
            }
            else
            {
                // This is a simple integer
                typeDeclaration.SetDotnetTypeName("JsonInteger");
            }

            return true;
        }

        return false;
    }
}