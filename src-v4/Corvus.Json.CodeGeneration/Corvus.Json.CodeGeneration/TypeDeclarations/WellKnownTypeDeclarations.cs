// <copyright file="WellKnownTypeDeclarations.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Well known type declarations.
/// </summary>
public static class WellKnownTypeDeclarations
{
    /// <summary>
    /// Gets the JsonAny type declaration.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>{}</c> or <c>true</c>.
    /// </remarks>
    public static TypeDeclaration JsonAny { get; } = CreateJsonAnyTypeDeclaration();

    /// <summary>
    /// Gets the JsonNotAny type declaration.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>{"not": true}</c> or <c>false</c>.
    /// </remarks>
    public static TypeDeclaration JsonNotAny { get; } = CreateJsonNotAnyTypeDeclaration();

    /// <summary>
    /// Try to get the type declaration for a boolean schema.
    /// </summary>
    /// <param name="schema">The schema for which to create the well-known type declaration.</param>
    /// <param name="booleanTypeDeclaration">The resulting type declaration, or <see langword="null"/>
    /// if it was not a boolean type declaration.</param>
    /// <returns><see langword="true"/> if the type declaration was found.</returns>
    public static bool TryGetBooleanOrEmptySchemaTypeDeclaration(LocatedSchema schema, [NotNullWhen(true)] out TypeDeclaration? booleanTypeDeclaration)
    {
        switch (schema.Schema.ValueKind)
        {
            case JsonValueKind.True:
                booleanTypeDeclaration = JsonAny;
                return true;
            case JsonValueKind.False:
                booleanTypeDeclaration = JsonNotAny;
                return true;
            case JsonValueKind.Object:
                if (!schema.Schema.EnumerateObject().Any())
                {
                    booleanTypeDeclaration = JsonAny;
                    return true;
                }

                booleanTypeDeclaration = null;
                return false;
            default:
                booleanTypeDeclaration = null;
                return false;
        }
    }

    private static TypeDeclaration CreateJsonNotAnyTypeDeclaration()
    {
        using var doc = JsonDocument.Parse("false");
        return new TypeDeclaration(new(
            new("corvus:/JsonNotAny"),
            doc.RootElement.Clone(),
            NullVocabulary.Instance))
        { BuildComplete = true };
    }

    private static TypeDeclaration CreateJsonAnyTypeDeclaration()
    {
        using var doc = JsonDocument.Parse("true");
        return new TypeDeclaration(new(
            new("corvus:/JsonAny"),
            doc.RootElement.Clone(),
            NullVocabulary.Instance))
        { BuildComplete = true };
    }
}