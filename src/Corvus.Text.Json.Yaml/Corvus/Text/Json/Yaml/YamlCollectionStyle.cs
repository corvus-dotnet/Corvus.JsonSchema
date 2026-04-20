// <copyright file="YamlCollectionStyle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Specifies the serialization style for YAML mappings and sequences.
/// </summary>
public enum YamlCollectionStyle
{
    /// <summary>Block style (indentation-based, one item per line).</summary>
    Block,

    /// <summary>Flow style (inline, comma-separated within brackets or braces).</summary>
    Flow,
}