// <copyright file="YamlSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Specifies the YAML schema used for tag resolution and scalar type coercion.
/// </summary>
public enum YamlSchema
{
    /// <summary>
    /// YAML 1.2 Core Schema (default). Recognizes null, bool, int (decimal, octal, hex),
    /// and float (decimal, .inf, .nan) with case-insensitive patterns.
    /// </summary>
    Core,

    /// <summary>
    /// YAML 1.2 JSON Schema. Strict JSON-compatible resolution: only
    /// <c>null</c>, <c>true</c>/<c>false</c>, and JSON-style numbers.
    /// </summary>
    Json,

    /// <summary>
    /// YAML 1.2 Failsafe Schema. All scalars are resolved as strings.
    /// No implicit type coercion is performed.
    /// </summary>
    Failsafe,

    /// <summary>
    /// YAML 1.1 compatibility mode. Recognizes yes/no/on/off/y/n as booleans,
    /// sexagesimal integers, and merge keys (&lt;&lt;).
    /// </summary>
    Yaml11,
}