// <copyright file="MaxItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The maxItems keyword.
/// </summary>
public sealed class MaxItemsKeyword : IArrayLengthConstantValidationKeyword
{
    private MaxItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MaxItemsKeyword"/> keyword.
    /// </summary>
    public static MaxItemsKeyword Instance { get; } = new MaxItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "maxItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "maxItems"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Array
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetOperator(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out Operator op)
    {
        if (typeDeclaration.HasKeyword(this))
        {
            op = Operator.LessThanOrEquals;
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

    /// <inheritdoc/>
    public bool TryGetValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out int value)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement jsonValue))
        {
            if (jsonValue.ValueKind == JsonValueKind.Number &&
                jsonValue.TryGetInt32(out int intValue))
            {
                value = intValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}