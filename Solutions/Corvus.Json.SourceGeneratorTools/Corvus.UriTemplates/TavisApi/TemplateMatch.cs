// <copyright file="TemplateMatch.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A template match.
/// </summary>
public class TemplateMatch
{
    /// <summary>
    /// Gets or sets the match key.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the matched template.
    /// </summary>
    public UriTemplate? Template { get; set; }

    /// <summary>
    /// Gets or sets the matched parameters.
    /// </summary>
    public IDictionary<string, object>? Parameters { get; set; }
}