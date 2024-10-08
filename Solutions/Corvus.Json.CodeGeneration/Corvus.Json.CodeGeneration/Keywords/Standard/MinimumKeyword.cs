// <copyright file="MinimumKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The minimum keyword.
/// </summary>
public sealed class MinimumKeyword : INumberConstantValidationKeyword
{
    private MinimumKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MinimumKeyword"/> keyword.
    /// </summary>
    public static MinimumKeyword Instance { get; } = new MinimumKeyword();

    /// <inheritdoc />
    public string Keyword => "minimum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "minimum"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
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
            if (typeDeclaration.HasExclusiveMinimumModifier())
            {
                op = Operator.GreaterThan;
            }
            else
            {
                op = Operator.GreaterThanOrEquals;
            }

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