// <copyright file="MaximumKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The maximum keyword.
/// </summary>
public sealed class MaximumKeyword : INumericConstantValidationKeyword
{
    private MaximumKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MaximumKeyword"/> keyword.
    /// </summary>
    public static MaximumKeyword Instance { get; } = new MaximumKeyword();

    /// <inheritdoc />
    public string Keyword => "maximum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "maximum"u8;

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
}