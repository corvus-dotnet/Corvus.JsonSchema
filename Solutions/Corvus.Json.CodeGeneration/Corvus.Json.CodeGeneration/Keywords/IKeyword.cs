// <copyright file="IKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword definition in a vocabulary.
/// </summary>
public interface IKeyword
{
    /// <summary>
    /// Gets the keyword as a string.
    /// </summary>
    string Keyword { get; }

    /// <summary>
    /// Gets the keyword as a UTF8 string.
    /// </summary>
    ReadOnlySpan<byte> KeywordUtf8 { get; }

    /// <summary>
    /// Gets a value indicating which core types are implied by the keyword given its value.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <returns>The core types implied by the keyword.</returns>
    CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether this keyword prevents the type from being
    /// reduced .
    /// </summary>
    /// <param name="schemaValue">The schema value containing the keyword.</param>
    /// <returns><see langword="true"/> if the schema can be reduced when this keyword is present.</returns>
    public bool CanReduce(in JsonElement schemaValue);
}