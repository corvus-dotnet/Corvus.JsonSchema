// <copyright file="YamlEventType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// The type of a YAML parse event.
/// </summary>
public enum YamlEventType
{
    /// <summary>Start of the YAML stream.</summary>
    StreamStart,

    /// <summary>End of the YAML stream.</summary>
    StreamEnd,

    /// <summary>Start of a YAML document.</summary>
    DocumentStart,

    /// <summary>End of a YAML document.</summary>
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
}