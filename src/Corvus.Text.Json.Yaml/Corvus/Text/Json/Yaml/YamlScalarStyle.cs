// <copyright file="YamlScalarStyle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// The style of a YAML scalar value.
/// </summary>
public enum YamlScalarStyle
{
    /// <summary>Plain (unquoted) scalar.</summary>
    Plain,

    /// <summary>Single-quoted scalar ('...').</summary>
    SingleQuoted,

    /// <summary>Double-quoted scalar ("...").</summary>
    DoubleQuoted,

    /// <summary>Literal block scalar (|).</summary>
    Literal,

    /// <summary>Folded block scalar (&gt;).</summary>
    Folded,
}