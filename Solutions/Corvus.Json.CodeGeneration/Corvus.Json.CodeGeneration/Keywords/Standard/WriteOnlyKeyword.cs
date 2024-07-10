// <copyright file="WriteOnlyKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The writeOnly keyword.
/// </summary>
public sealed class WriteOnlyKeyword : INonStructuralKeyword
{
    private WriteOnlyKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="WriteOnlyKeyword"/> keyword.
    /// </summary>
    public static WriteOnlyKeyword Instance { get; } = new WriteOnlyKeyword();

    /// <inheritdoc />
    public string Keyword => "writeOnly";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "writeOnly"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}