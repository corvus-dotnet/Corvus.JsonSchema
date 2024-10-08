// <copyright file="DollarAnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $anchor keyword.
/// </summary>
public sealed class DollarAnchorKeyword : IAnchorKeyword
{
    private DollarAnchorKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarAnchorKeyword"/> keyword.
    /// </summary>
    public static DollarAnchorKeyword Instance { get; } = new DollarAnchorKeyword();

    /// <inheritdoc />
    public string Keyword => "$anchor";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$anchor"u8;

    /// <inheritdoc />
    public void AddAnchor(JsonSchemaRegistry jsonSchemaRegistry, JsonElement schema, JsonReference currentLocation)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            string? anchorName = value.GetString();
            if (anchorName != null &&
                jsonSchemaRegistry.TryGetSchemaAndBaseForLocation(
                    currentLocation,
                    out LocatedSchema? baseSchema,
                    out LocatedSchema? anchoredSchema))
            {
                baseSchema.AddOrUpdateLocatedAnchor(new NamedLocatedAnchor(anchorName, anchoredSchema));
            }
        }
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool TryApplyScopeToExistingType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out Anchors.ApplyScopeResult result)
    {
        result = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryApplyScopeToNewType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        return false;
    }
}