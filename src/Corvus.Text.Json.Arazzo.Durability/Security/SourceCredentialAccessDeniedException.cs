// <copyright file="SourceCredentialAccessDeniedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when cataloguing a workflow is refused because it declares one or more sources whose credential bindings the
/// submitter is not entitled to use (design §13): the workflow's runs would never receive those credentials, so the
/// submission is rejected at catalog time rather than silently failing at run time. Maps to HTTP 400/403 at the
/// control-plane surface.
/// </summary>
public sealed class SourceCredentialAccessDeniedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SourceCredentialAccessDeniedException"/> class.</summary>
    /// <param name="deniedSources">The declared source names the submitter is not entitled to use.</param>
    public SourceCredentialAccessDeniedException(IReadOnlyList<string> deniedSources)
        : base($"The workflow declares source credential-protected source(s) the submitter is not entitled to use: {string.Join(", ", deniedSources)}.")
        => this.DeniedSources = deniedSources;

    /// <summary>Gets the declared source names the submitter is not entitled to use.</summary>
    public IReadOnlyList<string> DeniedSources { get; }
}