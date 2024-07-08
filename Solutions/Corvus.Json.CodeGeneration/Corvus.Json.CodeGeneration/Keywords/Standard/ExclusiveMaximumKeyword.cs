// <copyright file="ExclusiveMaximumKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The exclusiveMaximum keyword.
/// </summary>
public sealed class ExclusiveMaximumKeyword : INumericConstantValidationKeyword
{
    private ExclusiveMaximumKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExclusiveMaximumKeyword"/> keyword.
    /// </summary>
    public static ExclusiveMaximumKeyword Instance { get; } = new ExclusiveMaximumKeyword();

    /// <inheritdoc />
    public string Keyword => "exclusiveMaximum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "exclusiveMaximum"u8;

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
            op = Operator.LessThan;
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