// <copyright file="DraftRuns.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The identity conventions for §18 <em>draft runs</em> (workflow-designer design §18, staging item 3): durable
/// runs of a working copy's captured document, riding the ordinary runs machinery. A draft run is an ordinary
/// <see cref="WorkflowRun"/> whose <see cref="WorkflowRun.WorkflowId"/> is the reserved <see cref="RunWorkflowId"/>
/// (drafts have no catalogued <c>{base}-v{n}</c> identity); its captured draft — the document, its sources, and
/// the §18 audit tuple — lives in the sibling <see cref="IDraftRunStore"/>, keyed by the run id, exactly as a
/// catalog run's executable lives in the catalog rather than the run record.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Dispatch.</strong> Because every draft run shares the one reserved workflow id, a runner declares
/// draft hosting by passing <see cref="RunWorkflowId"/> among the hosted workflow ids it dispatches for — the
/// store-as-queue filter and the §5.5 <c>runnerEnvironment</c> pinning then apply unchanged, so a draft run
/// pinned to environment <em>E</em> is claimable only by a draft-hosting runner serving <em>E</em>.
/// </para>
/// <para>
/// <strong>Visibility.</strong> Draft runs never surface on the production runs listing: an unfiltered
/// <see cref="IWorkflowWaitIndex.QueryAsync"/> structurally excludes rows carrying the reserved id (a caller must
/// ask for <see cref="RunWorkflowId"/> explicitly), and each draft run is row-scoped to its working copy via the
/// internal <see cref="WorkingCopyTagKey"/> security tag (§14.2) — the working-copy analogue of
/// <see cref="WorkflowIdentity.WorkflowTagKey"/>.
/// </para>
/// </remarks>
public static class DraftRuns
{
    /// <summary>
    /// The reserved workflow id every draft run carries (the <c>$</c> prefix marks reserved names, mirroring the
    /// package's <c>$workflow</c>/<c>$executor</c> document names). It can never collide with a catalogued
    /// versioned id — the catalog rewrite always produces <c>{base}-v{n}</c>.
    /// </summary>
    public const string RunWorkflowId = "$draft";

    /// <summary>
    /// The reserved internal tag key carrying the working copy a draft run belongs to (design §14.2/§18) — set by
    /// the platform at start, never user-editable, so reach to a draft run is grantable per working copy.
    /// </summary>
    public const string WorkingCopyTagKey = "sys:workingCopy";

    /// <summary>Whether a run's workflow id marks it as a §18 draft run.</summary>
    /// <param name="workflowId">The run's workflow id.</param>
    /// <returns><see langword="true"/> for a draft run.</returns>
    public static bool IsDraftRun(string workflowId) => string.Equals(workflowId, RunWorkflowId, StringComparison.Ordinal);

    /// <summary>Builds the working-copy identity security tag for a draft run.</summary>
    /// <param name="workingCopyId">The working copy id.</param>
    /// <returns>The <c>sys:workingCopy</c> tag.</returns>
    public static SecurityTag WorkingCopyTag(string workingCopyId) => new(WorkingCopyTagKey, workingCopyId);

    /// <summary>Returns <paramref name="callerTags"/> with the working-copy identity tag for
    /// <paramref name="workingCopyId"/> added — the draft run's full security tag set (the starter's ambient
    /// identity plus the working-copy scope), mirroring <see cref="WorkflowIdentity.WithWorkflowTag"/>.</summary>
    /// <param name="callerTags">The starter's ambient identity tags (e.g. <c>sys:tenant=acme</c>).</param>
    /// <param name="workingCopyId">The working copy id.</param>
    /// <returns>The combined tag set.</returns>
    public static SecurityTagSet WithWorkingCopyTag(SecurityTagSet callerTags, string workingCopyId)
    {
        List<SecurityTag> tags = callerTags.ToList();
        tags.Add(WorkingCopyTag(workingCopyId));
        return SecurityTagSet.FromTags(tags);
    }
}