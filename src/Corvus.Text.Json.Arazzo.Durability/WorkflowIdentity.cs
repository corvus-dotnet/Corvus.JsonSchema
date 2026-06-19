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
/// <strong>Allocation ledger.</strong> <see cref="SameAdministrator"/> (the membership comparison, called in nested loops
/// when authorizing/mutating administration) compares the two sets directly on their unescaped UTF-8 tag bytes
/// (<see cref="SecurityTagSet.SetEquals"/>) — no managed strings, no list, no hash — instead of materializing two tag
/// <see cref="List{T}"/>s plus their strings per call. <see cref="AdministratorIdentity"/> filters the workflow tag via the holder's allocation-free enumerator (one
/// list, not two plus a LINQ iterator). <see cref="WithWorkflowTag"/> is the per-version <em>publish</em> path (cold)
/// and keeps the straightforward one-list <see cref="SecurityTagSet.FromTags"/> form.
/// </remarks>
public static class WorkflowIdentity
{
    /// <summary>The reserved internal tag key carrying a workflow's base id.</summary>
    public const string WorkflowTagKey = "sys:workflow";

    /// <summary>Builds the workflow-identity security tag for a base workflow id.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <returns>The <c>sys:workflow</c> tag.</returns>
    public static SecurityTag WorkflowTag(string baseWorkflowId) => new(WorkflowTagKey, baseWorkflowId);

    /// <summary>Returns <paramref name="ownerTags"/> with the workflow-identity tag for <paramref name="baseWorkflowId"/>
    /// added — the version's full security tag set (owner identity + immutable workflow identity).</summary>
    /// <param name="ownerTags">The owner-identity tags (e.g. <c>sys:tenant=acme</c>).</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <returns>The combined tag set.</returns>
    public static SecurityTagSet WithWorkflowTag(SecurityTagSet ownerTags, string baseWorkflowId)
    {
        List<SecurityTag> tags = ownerTags.ToList();
        tags.Add(WorkflowTag(baseWorkflowId));
        return SecurityTagSet.FromTags(tags);
    }

    /// <summary>Returns the administrator-identity portion of a version's tags — the full stamped <c>sys:</c> identity
    /// with the workflow-identity tag removed — used to compare administration when a new version is added to an
    /// existing base id.</summary>
    /// <param name="versionTags">A catalogued version's security tags.</param>
    /// <returns>The administrator-identity tags.</returns>
    public static SecurityTagSet AdministratorIdentity(SecurityTagSet versionTags)
    {
        // Filter out the workflow-identity tag via the allocation-free enumerator — one list (the FromTags input), not a
        // ToList + LINQ Where + a second ToList.
        var tags = new List<SecurityTag>();
        foreach (SecurityTag tag in versionTags)
        {
            if (!string.Equals(tag.Key, WorkflowTagKey, StringComparison.Ordinal))
            {
                tags.Add(tag);
            }
        }

        return SecurityTagSet.FromTags(tags);
    }

    /// <summary>Whether two administrator identities are equal as sets (order-independent), the administration
    /// membership comparison.</summary>
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