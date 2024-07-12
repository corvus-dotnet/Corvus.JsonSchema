// <copyright file="MaximumWithBooleanExclusiveKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The maximum keyword.
/// </summary>
public sealed class MaximumWithBooleanExclusiveKeyword : INumericConstantValidationKeyword
{
    private MaximumWithBooleanExclusiveKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="MaximumKeyword"/> keyword.
    /// </summary>
    public static MaximumWithBooleanExclusiveKeyword Instance { get; } = new MaximumWithBooleanExclusiveKeyword();

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
            if (typeDeclaration.Keywords()
                .OfType<IExclusiveMaximumBooleanKeyword>()
                .Any(k => typeDeclaration.TryGetKeyword(k, out JsonElement value) && value.ValueKind == JsonValueKind.True))
            {
                op = Operator.LessThan;
            }
            else
            {
                op = Operator.LessThanOrEquals;
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