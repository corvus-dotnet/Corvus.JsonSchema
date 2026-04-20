// <copyright file="YamlScalarStyle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Yaml.Internal;

/// <summary>
/// The style of a YAML scalar value.
/// </summary>
internal enum YamlScalarStyle
{
    /// <summary>Plain (unquoted) scalar.</summary>
    Plain,

    /// <summary>Single-quoted scalar ('...').</summary>
    SingleQuoted,

    /// <summary>Double-quoted scalar ("...").</summary>
    DoubleQuoted,

    /// <summary>Literal block scalar (|).</summary>
    Literal,

    /// <summary>Folded block scalar (>).</summary>
    Folded,
}