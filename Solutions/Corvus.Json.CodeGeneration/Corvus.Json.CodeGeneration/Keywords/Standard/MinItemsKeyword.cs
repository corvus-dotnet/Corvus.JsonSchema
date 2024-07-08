// <copyright file="MinItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The minItems keyword.
/// </summary>
public sealed class MinItemsKeyword : IArrayLengthConstantValidationKeyword
{
    private MinItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MinItemsKeyword"/> keyword.
    /// </summary>
    public static MinItemsKeyword Instance { get; } = new MinItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "minItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "minItems"u8;

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
            op = Operator.GreaterThanOrEquals;
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