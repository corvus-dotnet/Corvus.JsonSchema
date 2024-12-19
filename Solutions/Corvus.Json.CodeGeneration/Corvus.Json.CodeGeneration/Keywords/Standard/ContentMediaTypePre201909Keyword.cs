// <copyright file="ContentMediaTypePre201909Keyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The contentMediaType keyword.
/// </summary>
public sealed class ContentMediaTypePre201909Keyword
    : IContentMediaTypeValidationKeyword
{
    private ContentMediaTypePre201909Keyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ContentMediaTypeKeyword"/> keyword.
    /// </summary>
    public static ContentMediaTypePre201909Keyword Instance { get; } = new ContentMediaTypePre201909Keyword();

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
    public bool TryGetFormat(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? format)
    {
        if (this.TryGetContentMediaType(typeDeclaration, out string? contentMediaType))
        {
            if (contentMediaType == "application/json")
            {
                if (typeDeclaration.ExplicitContentEncoding() is string contentEncoding)
                {
                    if (contentEncoding == "base64")
                    {
                        format = "corvus-base64-content-pre201909";
                        return true;
                    }
                    else
                    {
                        format = null;
                        return false;
                    }
                }
                else
                {
                    format = "corvus-json-content-pre201909";
                    return true;
                }
            }
            else if (
                contentMediaType == "application/octet-stream" &&
                typeDeclaration.ExplicitContentEncoding() is string contentEncoding &&
                contentEncoding == "base64")
            {
                format = "corvus-base64-content";
                return true;
            }
        }

        format = null;
        return false;
    }
}