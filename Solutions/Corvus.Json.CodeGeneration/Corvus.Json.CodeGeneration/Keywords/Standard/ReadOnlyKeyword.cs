// <copyright file="ReadOnlyKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The readOnly keyword.
/// </summary>
public sealed class ReadOnlyKeyword : IKeyword
{
    private ReadOnlyKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ReadOnlyKeyword"/> keyword.
    /// </summary>
    public static ReadOnlyKeyword Instance { get; } = new ReadOnlyKeyword();

    /// <inheritdoc />
    public string Keyword => "readOnly";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "readOnly"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}