// <copyright file="ContentEncodingKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The contentEncoding keyword.
/// </summary>
public sealed class ContentEncodingKeyword
    : IContentEncodingValidationKeyword
{
    private ContentEncodingKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ContentEncodingKeyword"/> keyword.
    /// </summary>
    public static ContentEncodingKeyword Instance { get; } = new ContentEncodingKeyword();

    /// <inheritdoc />
    public string Keyword => "contentEncoding";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "contentEncoding"u8;

    /// <inheritdoc/>
    public ContentEncodingSemantics ContentSemantics => ContentEncodingSemantics.Draft102909AndLater;

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
    public bool TryGetContentEncoding(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? contentEncoding)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
        {
            contentEncoding = value.GetString();
            return contentEncoding is not null;
        }

        contentEncoding = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetFormat(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? format)
    {
        if (this.TryGetContentEncoding(typeDeclaration, out string? contentEncoding)
            && contentEncoding == "base64" &&
            typeDeclaration.ExplicitContentMediaType() is null)
        {
            format = "corvus-base64-string";
            return true;
        }

        format = null;
        return false;
    }
}