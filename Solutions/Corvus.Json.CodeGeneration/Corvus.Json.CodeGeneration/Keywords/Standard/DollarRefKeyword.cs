﻿// <copyright file="DollarRefKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $ref keyword (which does not hide siblings).
/// </summary>
public sealed class DollarRefKeyword : IReferenceKeyword, ICompositionKeyword
{
    private const string KeywordPath = "#/$ref";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    /// <summary>
    /// Gets an instance of the <see cref="DollarRefKeyword"/> keyword.
    /// </summary>
    public static DollarRefKeyword Instance { get; } = new DollarRefKeyword();

    /// <inheritdoc/>
    public string Keyword => "$ref";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> KeywordUtf8 => "$ref"u8;

    /// <inheritdoc/>
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional, CancellationToken cancellationToken)
    {
        if (source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? subschema))
        {
            PropertyProvider.CollectProperties(
                subschema,
                target,
                visitedTypeDeclarations,
                treatRequiredAsOptional,
                cancellationToken);
        }
    }

    /// <inheritdoc/>
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? Composition.UnionImpliesCoreTypeForSubschema(SubschemaTypeDeclaration(typeDeclaration), KeywordPath, CoreTypes.None)
            : CoreTypes.None;

    /// <inheritdoc/>
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        if (schema.Schema.ValueKind == JsonValueKind.Object && schema.Schema.TryGetProperty(this.KeywordUtf8, out JsonElement value))
        {
            string referencePath = value.GetString() ?? throw new InvalidOperationException("The reference path cannot be null.");
            await References.ResolveStandardReference(typeBuilderContext, typeDeclaration, KeywordPathReference, referencePath, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            return [value];
        }

        return [];
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschema, int index)
    {
        Debug.Assert(index == 0, "The index must be 0 for a $ref keyword.");

        return KeywordPathReference.AppendFragment(subschema.ReducedPathModifier);
    }

    private static TypeDeclaration SubschemaTypeDeclaration(TypeDeclaration source) =>
        source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? subschema)
            ? subschema
            : throw new InvalidOperationException("The subschema type declaration is missing.");
}