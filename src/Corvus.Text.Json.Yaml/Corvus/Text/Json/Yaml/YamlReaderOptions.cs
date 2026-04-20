// <copyright file="YamlReaderOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Yaml;

/// <summary>
/// Options for configuring the YAML to JSON converter.
/// </summary>
public readonly struct YamlReaderOptions
{
    /// <summary>
    /// Gets the default options: Core schema, single-document required, error on duplicate keys,
    /// max alias expansion depth of 64, max alias expansion size of 1,000,000 nodes.
    /// </summary>
    public static readonly YamlReaderOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlReaderOptions"/> struct with default values.
    /// </summary>
    public YamlReaderOptions()
    {
        this.Schema = YamlSchema.Core;
        this.DocumentMode = YamlDocumentMode.SingleRequired;
        this.DuplicateKeyBehavior = DuplicateKeyBehavior.Error;
        this.MaxAliasExpansionDepth = 64;
        this.MaxAliasExpansionSize = 1_000_000;
    }

    /// <summary>
    /// Gets the YAML schema to use for tag resolution and scalar type coercion.
    /// Defaults to <see cref="YamlSchema.Core"/>.
    /// </summary>
    public YamlSchema Schema { get; init; }

    /// <summary>
    /// Gets the document mode specifying how multi-document streams are handled.
    /// Defaults to <see cref="YamlDocumentMode.SingleRequired"/>.
    /// </summary>
    public YamlDocumentMode DocumentMode { get; init; }

    /// <summary>
    /// Gets the behavior when duplicate mapping keys are encountered.
    /// Defaults to <see cref="DuplicateKeyBehavior.Error"/>.
    /// </summary>
    public DuplicateKeyBehavior DuplicateKeyBehavior { get; init; }

    /// <summary>
    /// Gets the maximum depth for alias expansion to prevent
    /// exponential expansion attacks (billion laughs).
    /// Defaults to 64.
    /// </summary>
    public int MaxAliasExpansionDepth { get; init; }

    /// <summary>
    /// Gets the maximum total number of nodes that can be produced
    /// by alias expansion. Defaults to 1,000,000.
    /// </summary>
    public int MaxAliasExpansionSize { get; init; }
}