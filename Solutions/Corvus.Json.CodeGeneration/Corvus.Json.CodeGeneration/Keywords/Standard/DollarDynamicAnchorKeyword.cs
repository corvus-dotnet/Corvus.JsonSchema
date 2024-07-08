// <copyright file="DollarDynamicAnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $dynamicAnchor keyword.
/// </summary>
public sealed class DollarDynamicAnchorKeyword : IAnchorKeyword
{
    private DollarDynamicAnchorKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarDynamicAnchorKeyword"/> keyword.
    /// </summary>
    public static DollarDynamicAnchorKeyword Instance { get; } = new DollarDynamicAnchorKeyword();

    /// <inheritdoc />
    public string Keyword => "$dynamicAnchor";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$dynamicAnchor"u8;

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
                baseSchema.AddOrUpdateLocatedAnchor(new DynamicLocatedAnchor(anchorName, anchoredSchema));
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// We cannot reduce if both we and the $id keyword are present.
    /// </remarks>
    public bool CanReduce(in JsonElement schemaValue)
    {
        return
            Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8) &&
            Reduction.CanReduceNonReducingKeyword(schemaValue, DollarIdKeyword.Instance.KeywordUtf8);
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool TryApplyScopeToExistingType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out Anchors.ApplyScopeResult result)
    {
        if (TryGetDynamicScope(typeBuilderContext, existingTypeDeclaration, out JsonReference? dynamicScope))
        {
            typeBuilderContext.EnterDynamicScope(typeDeclaration, existingTypeDeclaration, dynamicScope.Value, out TypeDeclaration? existingDynamicTypeDeclaration);

            if (existingDynamicTypeDeclaration is TypeDeclaration edt)
            {
                result = new(edt, true);
            }
            else
            {
                result = new(typeDeclaration, false);
            }

            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryApplyScopeToNewType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        // We don't do anything to the new type.
        return false;
    }

    private static bool TryGetDynamicScope(TypeBuilderContext typeBuilderContext, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out JsonReference? dynamicScope)
    {
        IReadOnlyCollection<string> dynamicReferences = References.GetDynamicReferences(typeBuilderContext, existingTypeDeclaration);

        if (dynamicReferences.Count == 0)
        {
            dynamicScope = null;
            return false;
        }

        foreach (string dynamicReference in dynamicReferences)
        {
            if (TryGetScopeForFirstDynamicAnchor(typeBuilderContext, dynamicReference, out JsonReference? baseScopeLocation))
            {
                // We have found a new dynamic anchor in the containing scope, so we cannot share a type
                // declaration with the previous instance.
                if (typeBuilderContext.TryGetPreviousScope(out JsonReference? location) && location == baseScopeLocation)
                {
                    dynamicScope = location;
                    return true;
                }
            }
        }

        dynamicScope = null;
        return false;
    }

    private static bool TryGetScopeForFirstDynamicAnchor(
        TypeBuilderContext typeBuilderContext,
        string anchor,
        [NotNullWhen(true)] out JsonReference? baseScopeLocation)
    {
        JsonSchemaScope? foundScope = null;

        foreach (JsonSchemaScope scope in typeBuilderContext.ReversedStack)
        {
            // Ignore consecutive identical scopes
            if (foundScope is JsonSchemaScope fs && fs.Location == scope.Location)
            {
                continue;
            }

            foundScope = scope;

            DynamicLocatedAnchor? a =
                scope.LocatedSchema.LocatedAnchors
                    .OfType<DynamicLocatedAnchor>()
                    .FirstOrDefault(a => a.Name == anchor);

            if (anchor is not null)
            {
                baseScopeLocation = scope.Location;
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }
}