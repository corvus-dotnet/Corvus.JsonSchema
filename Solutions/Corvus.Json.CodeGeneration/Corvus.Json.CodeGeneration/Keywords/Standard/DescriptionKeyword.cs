// <copyright file="DescriptionKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The description keyword.
/// </summary>
public sealed class DescriptionKeyword : ILongDocumentationProviderKeyword, INonStructuralKeyword
{
    private DescriptionKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DescriptionKeyword"/> keyword.
    /// </summary>
    public static DescriptionKeyword Instance { get; } = new DescriptionKeyword();

    /// <inheritdoc />
    public string Keyword => "description";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "description"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public bool TryGetLongDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? documentation)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement descriptionElement) &&
            descriptionElement.ValueKind == JsonValueKind.String)
        {
            documentation = descriptionElement.GetString();
            return documentation is not null;
        }

        documentation = null;
        return false;
    }
}