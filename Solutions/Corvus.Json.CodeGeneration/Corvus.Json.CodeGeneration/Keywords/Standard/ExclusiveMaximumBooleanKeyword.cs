// <copyright file="ExclusiveMaximumBooleanKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The exclusiveMaximum keyword.
/// </summary>
public sealed class ExclusiveMaximumBooleanKeyword : IExclusiveMaximumBooleanKeyword
{
    private ExclusiveMaximumBooleanKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExclusiveMaximumBooleanKeyword"/> keyword.
    /// </summary>
    public static ExclusiveMaximumBooleanKeyword Instance { get; } = new ExclusiveMaximumBooleanKeyword();

    /// <inheritdoc />
    public string Keyword => "exclusiveMaximum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "exclusiveMaximum"u8;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Number
            : CoreTypes.None;
}