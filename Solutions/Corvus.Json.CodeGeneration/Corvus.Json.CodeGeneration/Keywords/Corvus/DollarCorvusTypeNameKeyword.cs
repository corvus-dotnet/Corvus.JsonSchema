// <copyright file="DollarCorvusTypeNameKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $corvusTypeName keyword.
/// </summary>
public sealed class DollarCorvusTypeNameKeyword : ITypeNameProviderKeyword
{
    private DollarCorvusTypeNameKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarCorvusTypeNameKeyword"/> keyword.
    /// </summary>
    public static DollarCorvusTypeNameKeyword Instance { get; } = new DollarCorvusTypeNameKeyword();

    /// <inheritdoc />
    public string Keyword => "$corvusTypeName";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$corvusTypeName"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement corvustypenameValue) => true;
}