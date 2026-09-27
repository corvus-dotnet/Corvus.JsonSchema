// <copyright file="ControlPlaneSecurityHeadersOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// What a host adds to the control plane's browser security headers (ADR 0073). The policy itself is fixed; a host
/// names only the other origins its own deployment needs to reach.
/// </summary>
/// <remarks>
/// Every source is an origin, a scheme of <c>http</c> or <c>https</c> and a host with an optional port, and nothing
/// else: no path, no wildcard, no keyword. A source is checked when the headers are registered, so a value that would
/// widen the policy beyond one origin, or break out of its directive, stops the host at startup rather than being
/// emitted.
/// </remarks>
public sealed class ControlPlaneSecurityHeadersOptions
{
    /// <summary>
    /// Gets the origins, besides the host's own, the served pages may call with <c>fetch</c>. A host whose pages call a
    /// control plane on another origin names that origin here.
    /// </summary>
    public IList<string> ConnectSources { get; } = [];

    /// <summary>
    /// Gets the origins, besides the host's own, a form submission may reach, including every redirect it follows. A
    /// host whose sign-out form redirects to its identity provider's end-session endpoint names that provider here,
    /// because browsers enforce <c>form-action</c> on the redirect as well as on the submission.
    /// </summary>
    public IList<string> FormActionSources { get; } = [];

    /// <summary>
    /// Gets the origins permitted to frame the served pages. It is empty by default, and then nothing may frame them,
    /// which is what makes a framed click on a governance action impossible. A host that embeds the console in a
    /// portal of its own names the portal here, and <c>X-Frame-Options</c> is then not sent, since it cannot express an
    /// allowlist.
    /// </summary>
    public IList<string> FrameAncestors { get; } = [];

    /// <summary>
    /// Gets or sets how long a browser remembers to reach this host over HTTPS only. It is sent on HTTPS requests to a
    /// host that is not a loopback address. <see cref="TimeSpan.Zero"/> sends no <c>Strict-Transport-Security</c>.
    /// </summary>
    public TimeSpan HstsMaxAge { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets or sets a value indicating whether <c>Strict-Transport-Security</c> covers the host's subdomains. It is off by
    /// default, because the control plane may share a parent domain with hosts it does not govern.
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; }
}