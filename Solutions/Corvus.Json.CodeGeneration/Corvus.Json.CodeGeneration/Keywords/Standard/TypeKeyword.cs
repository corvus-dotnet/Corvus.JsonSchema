// <copyright file="TypeKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The type keyword.
/// </summary>
public sealed class TypeKeyword : ICoreTypeValidationKeyword
{
    private TypeKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="TypeKeyword"/> keyword.
    /// </summary>
    public static TypeKeyword Instance { get; } = new TypeKeyword();

    /// <inheritdoc />
    public string Keyword => "type";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "type"u8;

    /// <inheritdoc />
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                return Types.GetCoreTypesFor(value);
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                CoreTypes result = CoreTypes.None;

                foreach (JsonElement arrayValue in value.EnumerateArray())
                {
                    if (arrayValue.ValueKind == JsonValueKind.String)
                    {
                        result |= Types.GetCoreTypesFor(arrayValue);
                    }
                }

                return result;
            }
        }

        return CoreTypes.None;
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc/>
    public CoreTypes AllowedCoreTypes(TypeDeclaration typeDeclaration) => this.ImpliesCoreTypes(typeDeclaration);
}