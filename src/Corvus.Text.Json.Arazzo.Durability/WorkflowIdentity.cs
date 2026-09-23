// <copyright file="WorkflowIdentity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The immutable, server-assigned identity of a catalogued workflow as a security tag (design §13/§14.2): the
/// deployment-internal <c>sys:workflow</c> tag carrying the base workflow id. A catalogued version is stamped with it
/// and its runs inherit it, so a source credential binding can be granted to a <em>specific workflow</em> (by this
/// identity) and the entitlement cannot be self-granted — the tag is set from the catalogued version, never from the
/// Arazzo document the author wrote.
/// </summary>
/// <remarks>
/// <strong>Allocation ledger.</strong> <see cref="SameAdministrator"/> (the exact set-equality comparison for identity
/// operations — add-idempotency, dedupe, digest matching; the administration authorization gate is membership, §16.5.4)
/// compares the two sets directly on their unescaped UTF-8 tag bytes
/// (<see cref="SecurityTagSet.SetEquals"/>) — no managed strings, no list, no hash — instead of materializing two tag
/// <see cref="List{T}"/>s plus their strings per call. <see cref="VersionTags"/> is the per-version <em>publish</em>
/// path (cold) and keeps the straightforward one-list <see cref="SecurityTagSet.FromTags"/> form.
/// </remarks>
public static class WorkflowIdentity
{
    /// <summary>The reserved internal tag key carrying a workflow's base id.</summary>
    public const string WorkflowTagKey = "sys:workflow";

    /// <summary>Builds the workflow-identity security tag for a base workflow id.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <returns>The <c>sys:workflow</c> tag.</returns>
    public static SecurityTag WorkflowTag(string baseWorkflowId) => new(WorkflowTagKey, baseWorkflowId);

    /// <summary>Builds a catalogued version's full security tag set: the publisher's identity, the author's reach tags
    /// and the workflow-identity tag for <paramref name="baseWorkflowId"/>. The identity keeps its own meaning apart
    /// from this set: administration is established and checked from it alone (ADR 0007).</summary>
    /// <param name="identity">The publisher's deployment-stamped identity (e.g. <c>sys:tenant=acme</c>).</param>
    /// <param name="authorTags">The author's security tags, if any.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <returns>The combined tag set.</returns>
    public static SecurityTagSet VersionTags(SecurityTagSet identity, SecurityTagSet authorTags, string baseWorkflowId)
    {
        List<SecurityTag> tags = identity.ToList();
        foreach (SecurityTag tag in authorTags)
        {
            tags.Add(tag);
        }

        tags.Add(WorkflowTag(baseWorkflowId));
        return SecurityTagSet.FromTags(tags);
    }

    /// <summary>Whether a caller's tags carry an identity the deployment stamped: any tag in the reserved keyspace other
    /// than the workflow-identity tag. Every authenticated caller has one (its subject at least), and a caller in a
    /// posture that identifies nobody has none.</summary>
    /// <param name="callerTags">The caller's tags.</param>
    /// <returns><see langword="true"/> if the caller is identified.</returns>
    public static bool HasStampedIdentity(SecurityTagSet callerTags)
    {
        foreach (SecurityTag tag in callerTags)
        {
            if (tag.Key.StartsWith(SecurityShell.DefaultInternalPrefix, StringComparison.Ordinal)
                && !string.Equals(tag.Key, WorkflowTagKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether two administrator identities are equal as sets (order-independent) — the exact identity
    /// comparison for the identity operations (add-idempotency, dedupe, digest matching). The administration
    /// authorization gate uses membership (containment), not this; see the secured catalog's IsAdministeredByMember.</summary>
    /// <param name="a">The first set.</param>
    /// <param name="b">The second set.</param>
    /// <returns><see langword="true"/> if they contain exactly the same tags.</returns>
    public static bool SameAdministrator(SecurityTagSet a, SecurityTagSet b)
    {
        // Set-equality computed directly on the unescaped UTF-8 tag bytes (SecurityTagSet.SetEquals) — no managed
        // strings, no list, no hash; nothing escapes to the heap. It runs in nested loops when authorizing/mutating
        // administration, so the zero-allocation comparison matters.
        return a.SetEquals(b);
    }
}