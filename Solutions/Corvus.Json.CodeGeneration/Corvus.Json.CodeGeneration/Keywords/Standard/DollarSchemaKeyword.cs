// <copyright file="DollarSchemaKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $schema keyword.
/// </summary>
public sealed class DollarSchemaKeyword : IKeyword
{
    private DollarSchemaKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarSchemaKeyword"/> keyword.
    /// </summary>
    public static DollarSchemaKeyword Instance { get; } = new DollarSchemaKeyword();

    /// <inheritdoc />
    public string Keyword => "$schema";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$schema"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}