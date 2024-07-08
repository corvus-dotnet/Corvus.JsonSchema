// <copyright file="DollarCommentKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $comment keyword.
/// </summary>
public sealed class DollarCommentKeyword : IKeyword
{
    private DollarCommentKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarCommentKeyword"/> keyword.
    /// </summary>
    public static DollarCommentKeyword Instance { get; } = new DollarCommentKeyword();

    /// <inheritdoc />
    public string Keyword => "$comment";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$comment"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}