// <copyright file="CredentialSourceAccess.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Whether a set of security tags is entitled to use the credential bindings configured for an Arazzo source (design
/// §13). Used to gate cataloguing a workflow that declares the source <em>before</em> any run — the earlier of the two
/// usage checks (the run-time check at transport bind being the backstop).
/// </summary>
public enum CredentialSourceAccess
{
    /// <summary>The source has no credential bindings at all — it is unauthenticated (or bindings are added later), so
    /// declaring it is allowed.</summary>
    Unconfigured,

    /// <summary>At least one binding for the source is usable by the tags (label-superset) — access is granted.</summary>
    Granted,

    /// <summary>The source has bindings, but none is usable by the tags — access is denied (the tags are reaching for a
    /// credential they are not entitled to).</summary>
    Denied,
}