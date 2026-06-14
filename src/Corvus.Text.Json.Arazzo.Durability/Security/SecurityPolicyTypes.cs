// <copyright file="SecurityPolicyTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>The mutable content of a security rule (see <see cref="SecurityRuleDocument"/>) supplied on add/update.</summary>
/// <param name="Expression">The rule text in the security-rule grammar.</param>
/// <param name="Description">An optional human description.</param>
public readonly record struct SecurityRuleDefinition(string Expression, string? Description = null);

/// <summary>The mutable content of a binding (see <see cref="SecurityBindingDocument"/>) supplied on add/update.</summary>
/// <param name="ClaimType">The principal claim type this binding keys on (<c>"*"</c> matches any authenticated principal).</param>
/// <param name="ClaimValue">The required claim value; <see langword="null"/> matches any value of <paramref name="ClaimType"/>.</param>
/// <param name="Read">The read-verb grant.</param>
/// <param name="Write">The write-verb grant.</param>
/// <param name="Purge">The purge-verb grant.</param>
/// <param name="Order">Resolution order (ascending).</param>
/// <param name="Description">An optional human description.</param>
public readonly record struct SecurityBindingDefinition(
    string ClaimType,
    string? ClaimValue,
    VerbGrant Read,
    VerbGrant Write,
    VerbGrant Purge,
    int Order = 0,
    string? Description = null);

/// <summary>
/// A consistent point-in-time view of all rules and bindings plus a monotonic <paramref name="Generation"/> token
/// a resolver caches against: when the store's generation advances, the cached compiled policy is stale.
/// </summary>
/// <param name="Rules">All persisted rules.</param>
/// <param name="Bindings">All persisted bindings.</param>
/// <param name="Generation">A monotonically increasing token bumped on every mutation.</param>
public readonly record struct SecurityPolicySnapshot(
    IReadOnlyList<SecurityRuleDocument> Rules,
    IReadOnlyList<SecurityBindingDocument> Bindings,
    long Generation);