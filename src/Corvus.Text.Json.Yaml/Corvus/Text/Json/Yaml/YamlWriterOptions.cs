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
    /// Gets the default options: 2-space indentation, structural validation enabled.
    /// </summary>
    public static readonly YamlWriterOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriterOptions"/> struct with default values.
    /// </summary>
    public YamlWriterOptions()
    {
        this.IndentSize = 2;
        this.SkipValidation = false;
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
}