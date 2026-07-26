// <copyright file="XNullableKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The x-nullable keyword.
/// </summary>
/// <remarks>
/// OpenAPI 2.0 (Swagger) has no standard way to admit null values; the
/// <c>x-nullable</c> vendor extension is the widely-adopted pre-3.0 convention
/// (equivalent to OpenAPI 3.0's <c>nullable</c>).
/// </remarks>
public sealed class XNullableKeyword : ICoreTypeValidationKeyword
{
    private XNullableKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="XNullableKeyword"/> keyword.
    /// </summary>
    public static XNullableKeyword Instance { get; } = new XNullableKeyword();

    /// <inheritdoc />
    public string Keyword => "x-nullable";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "x-nullable"u8;

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