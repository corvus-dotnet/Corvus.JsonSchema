// <copyright file="YamlWriterOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Options for configuring the YAML writer.
/// </summary>
public readonly struct YamlWriterOptions
{
    /// <summary>
    /// Gets the default options: canonical YAML, 2-space indentation, structural validation enabled.
    /// </summary>
    public static readonly YamlWriterOptions Default = new();

    /// <summary>
    /// Gets options that produce KYAML output (a strict subset of YAML 1.2; Kubernetes KEP-5295)
    /// with 2-space indentation and structural validation enabled.
    /// </summary>
    /// <remarks>
    /// KYAML uses explicit <c>{ }</c> / <c>[ ]</c> delimiters laid out across indented lines,
    /// always double-quotes string values, and writes numbers, booleans, and <c>null</c> bare.
    /// The output is always valid YAML 1.2. See <see cref="YamlWriterFormat.Kyaml"/>.
    /// </remarks>
    public static readonly YamlWriterOptions Kyaml = new() { Format = YamlWriterFormat.Kyaml };

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriterOptions"/> struct with default values.
    /// </summary>
    public YamlWriterOptions()
    {
        this.IndentSize = 2;
        this.SkipValidation = false;
        this.Format = YamlWriterFormat.Yaml;
    }

    /// <summary>
    /// Gets the number of spaces to use for each indentation level.
    /// Defaults to 2.
    /// </summary>
    public int IndentSize { get; init; }

    /// <summary>
    /// Gets a value indicating whether to skip structural validation.
    /// When <see langword="false"/> (the default), the writer validates that
    /// write operations produce structurally valid YAML.
    /// </summary>
    public bool SkipValidation { get; init; }

    /// <summary>
    /// Gets the output dialect produced by the writer.
    /// Defaults to <see cref="YamlWriterFormat.Yaml"/>.
    /// </summary>
    public YamlWriterFormat Format { get; init; }
}