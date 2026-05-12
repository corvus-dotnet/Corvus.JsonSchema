// <copyright file="DiscriminatorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The discriminator keyword.
/// </summary>
public sealed class DiscriminatorKeyword : INonStructuralKeyword
{
    private DiscriminatorKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DiscriminatorKeyword"/> keyword.
    /// </summary>
    public static DiscriminatorKeyword Instance { get; } = new DiscriminatorKeyword();

    /// <inheritdoc />
    public string Keyword => "discriminator";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "discriminator"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}