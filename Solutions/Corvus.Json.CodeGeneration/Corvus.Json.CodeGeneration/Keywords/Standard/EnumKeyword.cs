// <copyright file="EnumKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The enum keyword.
/// </summary>
public sealed class EnumKeyword : IAnyOfConstantValidationKeyword
{
    private EnumKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="EnumKeyword"/> keyword.
    /// </summary>
    public static EnumKeyword Instance { get; } = new EnumKeyword();

    /// <inheritdoc />
    public string Keyword => "enum";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "enum"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array
            ? CoreTypesHelpers.FromValueKinds(GetValueKindsFromArray(value))
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetValidationConstants(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement[]? constants)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            constants = new JsonElement[value.GetArrayLength()];
            int i = 0;
            foreach (JsonElement element in value.EnumerateArray())
            {
                constants[i++] = element;
            }

            return true;
        }

        constants = null;
        return false;
    }

    private static JsonValueKind[] GetValueKindsFromArray(JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Array, "The value must be an array.");

        var result = new JsonValueKind[value.GetArrayLength()];
        int i = 0;
        foreach (JsonElement element in value.EnumerateArray())
        {
            result[i++] = element.ValueKind;
        }

        return result;
    }
}