// <copyright file="DeprecatedKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The deprecated keyword.
/// </summary>
public sealed class DeprecatedKeyword : IKeyword
{
    private DeprecatedKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DeprecatedKeyword"/> keyword.
    /// </summary>
    public static DeprecatedKeyword Instance { get; } = new DeprecatedKeyword();

    /// <inheritdoc />
    public string Keyword => "deprecated";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "deprecated"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}