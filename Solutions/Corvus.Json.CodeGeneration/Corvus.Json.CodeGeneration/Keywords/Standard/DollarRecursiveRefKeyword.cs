// <copyright file="DollarRecursiveRefKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $recursiveRef keyword.
/// </summary>
public sealed class DollarRecursiveRefKeyword : IReferenceKeyword, IPropertyProviderKeyword
{
    private const string KeywordPath = "#/$recursiveRef";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    /// <summary>
    /// Gets an instance of the <see cref="DollarRecursiveRefKeyword"/> keyword.
    /// </summary>
    public static DollarRecursiveRefKeyword Instance { get; } = new DollarRecursiveRefKeyword();

    /// <inheritdoc/>
    public string Keyword => "$recursiveRef";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> KeywordUtf8 => "$recursiveRef"u8;

    /// <inheritdoc/>
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? subschema))
        {
            PropertyProvider.CollectProperties(
                subschema,
                target,
                visitedTypeDeclarations,
                treatRequiredAsOptional);
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
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? Composition.UnionImpliesCoreTypeForSubschema(SubschemaTypeDeclaration(typeDeclaration), KeywordPath, CoreTypes.None)
            : CoreTypes.None;

    /// <inheritdoc/>
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        if (schema.Schema.ValueKind == JsonValueKind.Object && schema.Schema.TryGetProperty(this.KeywordUtf8, out JsonElement value))
        {
            string referencePath = value.GetString() ?? throw new InvalidOperationException("The reference path cannot be null.");
            await References.ResolveRecursiveReference(typeBuilderContext, typeDeclaration, KeywordPathReference, referencePath).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschema, int index)
    {
        Debug.Assert(index == 0, "The index must be 0 for a $recursiveRef keyword");
        return KeywordPathReference.AppendFragment(subschema.ReducedPathModifier);
    }

    private static TypeDeclaration SubschemaTypeDeclaration(TypeDeclaration source) =>
        source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? subschema)
            ? subschema
            : throw new InvalidOperationException("The subschema type declaration is missing.");
}