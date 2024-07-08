// <copyright file="DollarVocabularyKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $vocabulary keyword.
/// </summary>
public sealed class DollarVocabularyKeyword : IKeyword
{
    private DollarVocabularyKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarVocabularyKeyword"/> keyword.
    /// </summary>
    public static DollarVocabularyKeyword Instance { get; } = new DollarVocabularyKeyword();

    /// <inheritdoc />
    public string Keyword => "$vocabulary";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$vocabulary"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}