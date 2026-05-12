// <copyright file="WriteOnlyKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The writeOnly keyword.
/// </summary>
public sealed class WriteOnlyKeyword : INonStructuralKeyword, IAnnotationProducingKeyword
{
    private WriteOnlyKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="WriteOnlyKeyword"/> keyword.
    /// </summary>
    public static WriteOnlyKeyword Instance { get; } = new WriteOnlyKeyword();

    /// <inheritdoc />
    public string Keyword => "writeOnly";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "writeOnly"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

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