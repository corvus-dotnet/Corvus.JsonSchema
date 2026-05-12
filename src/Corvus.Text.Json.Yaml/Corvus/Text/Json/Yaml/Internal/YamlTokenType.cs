// <copyright file="YamlTokenType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

/// <summary>
/// The type of a YAML token produced by the scanner.
/// </summary>
internal enum YamlTokenType
{
    /// <summary>No token (default/uninitialized).</summary>
    None,

    /// <summary>Start of the YAML stream.</summary>
    StreamStart,

    /// <summary>End of the YAML stream.</summary>
    StreamEnd,

    /// <summary>Start of a YAML document (explicit or implicit).</summary>
    DocumentStart,

    /// <summary>End of a YAML document (explicit or implicit).</summary>
    DocumentEnd,

    /// <summary>Start of a mapping (block or flow).</summary>
    MappingStart,

    /// <summary>End of a mapping.</summary>
    MappingEnd,

    /// <summary>Start of a sequence (block or flow).</summary>
    SequenceStart,

    /// <summary>End of a sequence.</summary>
    SequenceEnd,

    /// <summary>A scalar value.</summary>
    Scalar,

    /// <summary>An alias reference (*name).</summary>
    Alias,

    /// <summary>An anchor definition (&amp;name).</summary>
    Anchor,

    /// <summary>A tag (!tag or !!tag).</summary>
    Tag,
}