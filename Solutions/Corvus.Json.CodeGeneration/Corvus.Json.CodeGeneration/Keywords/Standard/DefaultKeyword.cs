// <copyright file="DefaultKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The default keyword.
/// </summary>
public sealed class DefaultKeyword : IDefaultValueProviderKeyword
{
    private DefaultKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DefaultKeyword"/> keyword.
    /// </summary>
    public static DefaultKeyword Instance { get; } = new DefaultKeyword();

    /// <inheritdoc />
    public string Keyword => "default";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "default"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypesHelpers.FromValueKind(typeDeclaration.DefaultValue().ValueKind)
            : CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc/>
    public bool TryGetExamples(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string[]? examples)
    {
        if (this.TryGetDefaultValue(typeDeclaration, out JsonElement defaultElement))
        {
            string example = defaultElement.GetRawText();
            examples = [example];
            return true;
        }

        examples = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetDefaultValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement defaultValue)
    {
        return typeDeclaration.TryGetKeyword(this, out defaultValue);
    }
}