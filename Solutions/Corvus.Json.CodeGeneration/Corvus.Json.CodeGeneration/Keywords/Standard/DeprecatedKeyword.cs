// <copyright file="DeprecatedKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The deprecated keyword.
/// </summary>
public sealed class DeprecatedKeyword : IDeprecatedKeyword
{
    private DeprecatedKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DeprecatedKeyword"/> keyword.
    /// </summary>
    public static DeprecatedKeyword Instance { get; } = new DeprecatedKeyword();

    /// <inheritdoc />
    public string Keyword => "deprecated";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "deprecated"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc />
    public bool IsDeprecated(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out string? message)
    {
        // This keyword doesn't support an explicit deprecation message.
        message = null;
        return typeDeclaration.TryGetKeyword(this, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    }
}