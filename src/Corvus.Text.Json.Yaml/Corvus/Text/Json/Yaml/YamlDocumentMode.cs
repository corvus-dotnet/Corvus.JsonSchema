// <copyright file="YamlDocumentMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Yaml;

/// <summary>
/// Specifies how multi-document YAML streams are handled.
/// </summary>
public enum YamlDocumentMode
{
    /// <summary>
    /// Require a single document. If the stream contains more than one document,
    /// a <see cref="YamlException"/> is thrown.
    /// </summary>
    SingleRequired,

    /// <summary>
    /// Multiple documents are wrapped in a JSON array.
    /// A single document stream produces a one-element array.
    /// </summary>
    MultiAsArray,
}