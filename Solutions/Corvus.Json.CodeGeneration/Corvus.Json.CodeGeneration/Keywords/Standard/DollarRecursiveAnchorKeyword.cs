// <copyright file="DollarRecursiveAnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $recursiveAnchor keyword.
/// </summary>
public sealed class DollarRecursiveAnchorKeyword : IRecursiveAnchorKeyword
{
    private const string RecursiveScopeKey = "$recursiveAnchor.RecursiveScope";

    private DollarRecursiveAnchorKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarRecursiveAnchorKeyword"/> keyword.
    /// </summary>
    public static DollarRecursiveAnchorKeyword Instance { get; } = new DollarRecursiveAnchorKeyword();

    /// <inheritdoc />
    public string Keyword => "$recursiveAnchor";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$recursiveAnchor"u8;

    /// <inheritdoc />
    public void AddAnchor(JsonSchemaRegistry jsonSchemaRegistry, JsonElement schema, JsonReference currentLocation)
    {
        // NOP
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool IsRecursiveAnchor(in JsonElement schema)
    {
        return schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty(this.KeywordUtf8, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    }

    /// <inheritdoc />
    public bool TryApplyScopeToExistingType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out Anchors.ApplyScopeResult result)
    {
        if (this.TryGetRecursiveScope(typeBuilderContext, existingTypeDeclaration, out JsonReference? dynamicScope))
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
    public bool TryGetScopeForFirstRecursiveAnchor(TypeBuilderContext typeBuilderContext, [NotNullWhen(true)] out JsonReference? baseScopeLocation)
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

            if (this.IsRecursiveAnchor(scope.LocatedSchema.Schema))
            {
                baseScopeLocation = scope.Location;
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryApplyScopeToNewType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (this.TryGetScopeForFirstRecursiveAnchor(typeBuilderContext, out JsonReference? baseScopeLocation))
        {
            // Set the recursive scope if we have one, for the root entity.
            typeDeclaration.SetMetadata(RecursiveScopeKey, baseScopeLocation.Value);
            return true;
        }

        return false;
    }

    private bool TryGetRecursiveScope(TypeBuilderContext typeBuilderContext, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out JsonReference? recursiveScope)
    {
        bool hasRecursiveReferences = References.HasRecursiveReferences(typeBuilderContext, existingTypeDeclaration);

        if (!hasRecursiveReferences)
        {
            recursiveScope = null;
            return false;
        }

        if (this.TryGetScopeForFirstRecursiveAnchor(typeBuilderContext, out JsonReference? baseScopeLocation) &&
            existingTypeDeclaration.TryGetMetadata(RecursiveScopeKey, out JsonReference scope) &&
            scope != baseScopeLocation)
        {
            recursiveScope = baseScopeLocation;
            return true;
        }

        recursiveScope = null;
        return false;
    }
}