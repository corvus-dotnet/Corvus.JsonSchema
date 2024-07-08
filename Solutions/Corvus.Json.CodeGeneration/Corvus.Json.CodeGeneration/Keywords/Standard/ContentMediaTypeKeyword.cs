// <copyright file="ContentMediaTypeKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The contentMediaType keyword.
/// </summary>
public sealed class ContentMediaTypeKeyword
    : IContentMediaTypeProviderKeyword, IStringValidationKeyword
{
    private ContentMediaTypeKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ContentMediaTypeKeyword"/> keyword.
    /// </summary>
    public static ContentMediaTypeKeyword Instance { get; } = new ContentMediaTypeKeyword();

    /// <inheritdoc />
    public string Keyword => "contentMediaType";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "contentMediaType"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.String
            : CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc/>
    public bool TryGetContentMediaType(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? contentMediaType)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String &&
            value.GetString() is string mediaType)
        {
            contentMediaType = mediaType;
            return true;
        }

        contentMediaType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool RequiresStringLength(TypeDeclaration typeDeclaration) => false;
}