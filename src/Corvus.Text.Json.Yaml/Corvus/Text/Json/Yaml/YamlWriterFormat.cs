// <copyright file="YamlWriterFormat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Specifies the output dialect produced by the YAML writer.
/// </summary>
public enum YamlWriterFormat
{
    /// <summary>
    /// Canonical YAML (the default). Block style by default, with scalars quoted
    /// only when required to round-trip safely.
    /// </summary>
    Yaml,

    /// <summary>
    /// KYAML — a strict subset of YAML 1.2 (Kubernetes KEP-5295) intended for
    /// unambiguous, predictable output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In this mode every mapping is rendered with explicit <c>{ }</c> braces and
    /// every sequence with explicit <c>[ ]</c> brackets, laid out across multiple
    /// indented lines with a trailing comma after every element. String values are
    /// always double-quoted; numbers, booleans, and <c>null</c> are written bare.
    /// Mapping keys are quoted only when they would otherwise be ambiguous.
    /// </para>
    /// <para>
    /// KYAML output is always valid YAML 1.2, so it can be read by any conforming
    /// YAML parser (including this library's reader).
    /// </para>
    /// </remarks>
    Kyaml,
}