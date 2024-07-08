// <copyright file="MultipleOfKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The multipleOf keyword.
/// </summary>
public sealed class MultipleOfKeyword : INumericConstantValidationKeyword
{
    private MultipleOfKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MultipleOfKeyword"/> keyword.
    /// </summary>
    public static MultipleOfKeyword Instance { get; } = new MultipleOfKeyword();

    /// <inheritdoc />
    public string Keyword => "multipleOf";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "multipleOf"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc/>
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Number
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetOperator(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out Operator op)
    {
        if (typeDeclaration.HasKeyword(this))
        {
            op = Operator.MultipleOf;
            return true;
        }

        op = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetValidationConstants(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement[]? constants)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            constants = [value];
            return true;
        }

        constants = null;
        return false;
    }
}