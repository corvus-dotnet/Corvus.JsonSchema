// <copyright file="TitleKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The title keyword.
/// </summary>
public sealed class TitleKeyword : IShortDocumentationProviderKeyword, INonStructuralKeyword, IAnnotationProducingKeyword
{
    private TitleKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="TitleKeyword"/> keyword.
    /// </summary>
    public static TitleKeyword Instance { get; } = new TitleKeyword();

    /// <inheritdoc />
    public string Keyword => "title";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "title"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public bool TryGetShortDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? documentation)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement titleElement) &&
            titleElement.ValueKind == JsonValueKind.String)
        {
            documentation = titleElement.GetString();
            return documentation is not null;
        }

        documentation = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetAnnotationJsonValue(TypeDeclaration typeDeclaration, out string rawJsonValue)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            rawJsonValue = value.GetRawText();
            return true;
        }

        rawJsonValue = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    public CoreTypes AnnotationAppliesToCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc/>
    public bool AnnotationPreconditionsMet(TypeDeclaration typeDeclaration) => true;
}