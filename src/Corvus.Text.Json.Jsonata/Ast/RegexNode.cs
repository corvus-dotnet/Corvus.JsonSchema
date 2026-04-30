// <copyright file="RegexNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A regular expression literal: <c>/pattern/flags</c>.
/// </summary>
internal sealed class RegexNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Regex;

    /// <summary>Gets or sets the regex pattern (without delimiters).</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Gets or sets the regex flags (e.g. <c>"im"</c>).</summary>
    public string Flags { get; set; } = string.Empty;
}