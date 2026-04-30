// <copyright file="ExternalDocsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The externaldocs keyword.
/// </summary>
public sealed class ExternalDocsKeyword : ILongDocumentationProviderKeyword, INonStructuralKeyword
{
    private ExternalDocsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExternalDocsKeyword"/> keyword.
    /// </summary>
    public static ExternalDocsKeyword Instance { get; } = new ExternalDocsKeyword();

    /// <inheritdoc />
    public string Keyword => "externalDocs";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "externalDocs"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public bool TryGetLongDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? documentation)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement externaldocsElement) &&
            externaldocsElement.ValueKind == JsonValueKind.Object)
        {
            if (externaldocsElement.TryGetProperty("description", out JsonElement descriptionElement) &&
                descriptionElement.ValueKind == JsonValueKind.String)
            {
                if (externaldocsElement.TryGetProperty("url", out JsonElement urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String)
                {
                    documentation = $"<see href=\"{urlElement.GetString()}\">{descriptionElement.GetString()}</see>";
                    return true;
                }
            }
            else if (externaldocsElement.TryGetProperty("url", out JsonElement urlElement) &&
                urlElement.ValueKind == JsonValueKind.String)
            {
                documentation = $"<see href=\"{urlElement.GetString()}\">External docmentation.</see>";
                return true;
            }
        }

        documentation = null;
        return false;
    }
}