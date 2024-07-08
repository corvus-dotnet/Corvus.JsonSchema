// <copyright file="UniqueItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The uniqueItems keyword.
/// </summary>
public sealed class UniqueItemsKeyword : IArrayValidationKeyword
{
    private const string KeywordPath = "#/uniqueItems";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private UniqueItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="UniqueItemsKeyword"/> keyword.
    /// </summary>
    public static UniqueItemsKeyword Instance { get; } = new UniqueItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "uniqueItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "uniqueItems"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Array
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => false;
}