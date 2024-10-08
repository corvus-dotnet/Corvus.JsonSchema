// <copyright file="ExclusiveMinimumBooleanKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The exclusiveMinimum boolean keyword.
/// </summary>
public sealed class ExclusiveMinimumBooleanKeyword : IExclusiveMinimumBooleanKeyword
{
    private ExclusiveMinimumBooleanKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExclusiveMinimumBooleanKeyword"/> keyword.
    /// </summary>
    public static ExclusiveMinimumBooleanKeyword Instance { get; } = new ExclusiveMinimumBooleanKeyword();

    /// <inheritdoc />
    public string Keyword => "exclusiveMinimum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "exclusiveMinimum"u8;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public bool HasModifier(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.TryGetKeyword(this, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Number
            : CoreTypes.None;
}