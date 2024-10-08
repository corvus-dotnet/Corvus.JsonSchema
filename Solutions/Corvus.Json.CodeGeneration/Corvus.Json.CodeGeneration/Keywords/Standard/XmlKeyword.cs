// <copyright file="XmlKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The xml keyword.
/// </summary>
public sealed class XmlKeyword : INonStructuralKeyword
{
    private XmlKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="XmlKeyword"/> keyword.
    /// </summary>
    public static XmlKeyword Instance { get; } = new XmlKeyword();

    /// <inheritdoc />
    public string Keyword => "xml";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "xml"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;
}