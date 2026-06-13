// <copyright file="SecurityPolicyTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted, named row-authorization rule (design §14.2): the rule <paramref name="Expression"/> in the
/// security-rule grammar (see <see cref="SecurityRule"/>) plus audit/concurrency metadata. Rules are referenced
/// by <paramref name="Name"/> from a <see cref="SecurityBinding"/>'s per-verb grants; changing a rule re-emits
/// its evaluator/predicate the next time a principal resolves.
/// </summary>
/// <param name="Name">The rule's stable identifier (unique within the store).</param>
/// <param name="Expression">The rule text in the security-rule grammar (compiles via <see cref="SecurityRule.Compile"/>).</param>
/// <param name="Description">An optional human description.</param>
/// <param name="CreatedBy">The actor that created the rule.</param>
/// <param name="CreatedAt">When the rule was created.</param>
/// <param name="UpdatedBy">The actor that last updated the rule, if it has been updated.</param>
/// <param name="UpdatedAt">When the rule was last updated, if it has been updated.</param>
/// <param name="Etag">The optimistic-concurrency token for update/delete.</param>
public readonly record struct SecurityRuleRecord(
    string Name,
    string Expression,
    string? Description,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    WorkflowEtag Etag);

/// <summary>The mutable content of a <see cref="SecurityRuleRecord"/> supplied on add/update.</summary>
/// <param name="Expression">The rule text in the security-rule grammar.</param>
/// <param name="Description">An optional human description.</param>
public readonly record struct SecurityRuleDefinition(string Expression, string? Description = null);

/// <summary>
/// A per-verb grant within a <see cref="SecurityBinding"/>: either <see cref="Unrestricted"/> access (a
/// <see langword="null"/> reach — the operator escape) or a set of rule names that are ANDed together (and then
/// composed with the deployment shell wrapper and OR-ed across all of a principal's matched bindings). The
/// default value (<see cref="Unrestricted"/> false, empty <see cref="RuleNames"/>) grants nothing for the verb.
/// </summary>
/// <param name="Unrestricted">When <see langword="true"/>, the verb is unrestricted (full reach) for a matched principal.</param>
/// <param name="RuleNames">The rule names ANDed for the verb (ignored when <see cref="Unrestricted"/> is <see langword="true"/>).</param>
public readonly record struct VerbGrant(bool Unrestricted, IReadOnlyList<string> RuleNames)
{
    /// <summary>Gets a grant that confers nothing (the verb is not granted by this binding).</summary>
    public static VerbGrant None => new(false, []);

    /// <summary>Gets a grant of unrestricted (full-reach) access for the verb.</summary>
    public static VerbGrant Full => new(true, []);

    /// <summary>Creates a grant of the conjunction of the named rules.</summary>
    /// <param name="ruleNames">The rule names (ANDed).</param>
    /// <returns>The grant.</returns>
    public static VerbGrant Rules(params string[] ruleNames) => new(false, ruleNames);

    /// <summary>Gets a value indicating whether this grant confers nothing.</summary>
    public bool IsEmpty => !this.Unrestricted && this.RuleNames.Count == 0;
}

/// <summary>
/// A persisted claim→rule mapping (design §14.2): for a principal carrying a matching claim, the per-verb grants
/// (read/write/purge) that resolve to its <see cref="AccessContext"/>. A principal may match several bindings;
/// for each verb the grants compose as OR (more bindings → more access), always within the deployment shell.
/// </summary>
/// <param name="Id">The binding's stable identifier (assigned by the store).</param>
/// <param name="ClaimType">The principal claim type this binding keys on (<c>"*"</c> matches any authenticated principal).</param>
/// <param name="ClaimValue">The required claim value; <see langword="null"/> matches any value of <paramref name="ClaimType"/>.</param>
/// <param name="Read">The read-verb grant.</param>
/// <param name="Write">The write-verb grant.</param>
/// <param name="Purge">The purge-verb grant.</param>
/// <param name="Order">Resolution order (ascending); lower runs first. Informational for OR composition, decisive only for display.</param>
/// <param name="Description">An optional human description.</param>
/// <param name="CreatedBy">The actor that created the binding.</param>
/// <param name="CreatedAt">When the binding was created.</param>
/// <param name="UpdatedBy">The actor that last updated the binding, if it has been updated.</param>
/// <param name="UpdatedAt">When the binding was last updated, if it has been updated.</param>
/// <param name="Etag">The optimistic-concurrency token for update/delete.</param>
public readonly record struct SecurityBinding(
    string Id,
    string ClaimType,
    string? ClaimValue,
    VerbGrant Read,
    VerbGrant Write,
    VerbGrant Purge,
    int Order,
    string? Description,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    WorkflowEtag Etag);

/// <summary>The mutable content of a <see cref="SecurityBinding"/> supplied on add/update.</summary>
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
    IReadOnlyList<SecurityRuleRecord> Rules,
    IReadOnlyList<SecurityBinding> Bindings,
    long Generation);