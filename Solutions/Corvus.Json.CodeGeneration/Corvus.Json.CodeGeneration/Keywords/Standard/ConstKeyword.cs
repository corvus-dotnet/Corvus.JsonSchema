// <copyright file="ConstKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The const keyword.
/// </summary>
public sealed class ConstKeyword : ISingleConstantValidationKeyword
{
    private const string KeywordPath = "#/const";

    private ConstKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ConstKeyword"/> keyword.
    /// </summary>
    public static ConstKeyword Instance { get; } = new ConstKeyword();

    /// <inheritdoc />
    public string Keyword => "const";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "const"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement value)
            ? CoreTypesHelpers.FromValueKind(value.ValueKind)
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetValidationConstants(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement[]? constants)
    {
        if (this.TryGetConstantValue(typeDeclaration, out JsonElement value))
        {
            constants = [value];
            return true;
        }

        constants = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetConstantValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement constantValue)
    {
        return typeDeclaration.TryGetKeyword(this, out constantValue);
    }

    /// <inheritdoc/>
    public string GetPathModifier()
    {
        return KeywordPath;
    }
}