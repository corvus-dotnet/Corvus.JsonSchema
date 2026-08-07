// <copyright file="YamlReaderOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Options for configuring the YAML to JSON converter.
/// </summary>
public readonly struct YamlReaderOptions
{
    /// <summary>The documented alias-expansion depth limit, applied when none is configured.</summary>
    internal const int DefaultMaxAliasExpansionDepth = 64;

    /// <summary>The documented alias-expansion size limit in bytes, applied when none is configured.</summary>
    internal const int DefaultMaxAliasExpansionSize = 1_000_000;

    /// <summary>
    /// Gets the default options: Core schema, single-document required, error on duplicate keys,
    /// max alias expansion depth of 64, max alias expansion size of 1,000,000 bytes.
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
        this.MaxAliasExpansionDepth = DefaultMaxAliasExpansionDepth;
        this.MaxAliasExpansionSize = DefaultMaxAliasExpansionSize;
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
    /// Gets the maximum total number of bytes that alias expansion may add to the
    /// output. Defaults to 1,000,000.
    /// </summary>
    public int MaxAliasExpansionSize { get; init; }

    /// <summary>Gets the alias-expansion depth limit to enforce, resolving an unset value to the documented default.</summary>
    internal int EffectiveMaxAliasExpansionDepth => this.MaxAliasExpansionDepth > 0 ? this.MaxAliasExpansionDepth : DefaultMaxAliasExpansionDepth;

    /// <summary>Gets the alias-expansion size limit to enforce, resolving an unset value to the documented default.</summary>
    internal int EffectiveMaxAliasExpansionSize => this.MaxAliasExpansionSize > 0 ? this.MaxAliasExpansionSize : DefaultMaxAliasExpansionSize;
}