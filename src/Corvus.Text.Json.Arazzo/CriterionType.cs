// <copyright file="CriterionType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The kind of an Arazzo Criterion Object (Arazzo Specification §Criterion Object).
/// </summary>
public enum CriterionType
{
    /// <summary>
    /// A simple condition combining runtime expressions and literals with the operators
    /// <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>&amp;&amp;</c>, and <c>||</c>.
    /// </summary>
    Simple,

    /// <summary>
    /// A regular expression matched against the criterion's context value.
    /// </summary>
    Regex,

    /// <summary>
    /// A JSONPath query (RFC 9535) evaluated against the criterion's context value; passes when the
    /// result node list is non-empty.
    /// </summary>
    JsonPath,
}