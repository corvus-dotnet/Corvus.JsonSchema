// <copyright file="NullableKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The nullable keyword.
/// </summary>
public sealed class NullableKeyword : ICoreTypeValidationKeyword
{
    private NullableKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="NullableKeyword"/> keyword.
    /// </summary>
    public static NullableKeyword Instance { get; } = new NullableKeyword();

    /// <inheritdoc />
    public string Keyword => "nullable";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "nullable"u8;

    /// <inheritdoc />
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) && value.ValueKind == JsonValueKind.True)
        {
            return CoreTypes.Null;
        }

        return CoreTypes.None;
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc/>
    public CoreTypes AllowedCoreTypes(TypeDeclaration typeDeclaration) => this.ImpliesCoreTypes(typeDeclaration);
}