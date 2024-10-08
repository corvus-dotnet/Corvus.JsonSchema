// <copyright file="WellKnownTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on the <see cref="WellKnownTypeDeclarations"/>.
/// </summary>
public sealed class WellKnownTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private WellKnownTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="WellKnownTypeNameHeuristic"/>.
    /// </summary>
    public static WellKnownTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.LocatedSchema.Location.Equals(WellKnownTypeDeclarations.JsonAny.LocatedSchema.Location))
        {
            typeDeclaration.SetDotnetNamespace("Corvus.Json");
            typeDeclaration.SetDotnetTypeName("JsonAny");
            written = 0;
            return true;
        }
        else if (typeDeclaration.LocatedSchema.Location.Equals(WellKnownTypeDeclarations.JsonNotAny.LocatedSchema.Location))
        {
            typeDeclaration.SetDotnetNamespace("Corvus.Json");
            typeDeclaration.SetDotnetTypeName("JsonNotAny");
            written = 0;
            return true;
        }

        written = 0;
        return false;
    }
}