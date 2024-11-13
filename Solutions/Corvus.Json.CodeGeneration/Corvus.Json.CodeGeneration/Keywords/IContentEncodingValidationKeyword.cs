// <copyright file="IContentEncodingValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword which provides a content encoding for a type declaration.
/// </summary>
public interface IContentEncodingValidationKeyword : IContentSemanticsKeyword, IFormatValidationKeyword
{
    /// <summary>
    /// Try to get the content encoding for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the content encoding.</param>
    /// <param name="contentEncoding">The content encoding, or <see langword="null"/> if not present.</param>
    /// <returns><see langword="true"/> if the content encoding was available on the type declaration.</returns>
    bool TryGetContentEncoding(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? contentEncoding);
}