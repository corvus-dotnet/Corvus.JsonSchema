﻿// <copyright file="DollarIdKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $id keyword.
/// </summary>
public sealed class DollarIdKeyword : IScopeKeyword, INonStructuralKeyword
{
    private DollarIdKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarIdKeyword"/> keyword.
    /// </summary>
    public static DollarIdKeyword Instance { get; } = new DollarIdKeyword();

    /// <inheritdoc />
    public string Keyword => "$id";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$id"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc />
    public bool TryEnterScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, string scopeName, out TypeDeclaration? existingTypeDeclaration)
    {
        return typeBuilderContext.TryEnterBaseScope(
            scopeName,
            typeDeclaration,
            out existingTypeDeclaration);
    }

    /// <inheritdoc />
    public bool TryGetScope(in JsonElement schemaValue, [NotNullWhen(true)] out string? scope)
    {
        if (schemaValue.ValueKind == JsonValueKind.Object && schemaValue.TryGetProperty(this.KeywordUtf8, out JsonElement value))
        {
            scope = value.GetString();
            return scope is not null;
        }

        scope = null;
        return false;
    }
}