// <copyright file="MinLengthKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The minLength keyword.
/// </summary>
public sealed class MinLengthKeyword : IStringLengthConstantValidationKeyword
{
    private MinLengthKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MinLengthKeyword"/> keyword.
    /// </summary>
    public static MinLengthKeyword Instance { get; } = new MinLengthKeyword();

    /// <inheritdoc />
    public string Keyword => "minLength";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "minLength"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.String
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresStringLength(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.HasKeyword(this);
    }

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
}