// <copyright file="ResumeClaimConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The §18 resume-claimable dispatch contract, shared by the in-scope reference stores (in-memory + SQLite):
/// a paused (or faulted) run carrying the resume-requested marker (<see cref="WorkflowRunIndexEntry.ResumeRequestedAt"/>)
/// is surfaced by <see cref="IWorkflowDispatchIndex.QueryClaimableAsync(IReadOnlyCollection{string}, string?, DateTimeOffset, CancellationToken)"/>
/// — in addition to <see cref="WorkflowRunStatus.Pending"/> and orphaned-<see cref="WorkflowRunStatus.Running"/>
/// runs — while respecting the hosted-workflow and environment filters, and a plainly-paused run (no marker) is not.
/// </summary>
/// <remarks>
/// This is deliberately NOT a method on the all-backend <see cref="WorkflowStateStoreConformance"/> base: the
/// marker's store fan-out to the other durability backends is a follow-on, so only the stores that have adopted
/// the marker column exercise this contract, via the thin per-store test classes that call these helpers.
/// </remarks>
public static class ResumeClaimConformance
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Asserts that only runs carrying the resume-requested marker (Suspended or Faulted) are surfaced —
    /// a plainly-paused Suspended run without the marker is not.</summary>
    /// <param name="store">The store under test (must implement <see cref="IWorkflowDispatchIndex"/>).</param>
    /// <returns>A task that completes when the assertions pass.</returns>
    public static async Task Surfaces_only_the_resume_requested_runs(IWorkflowStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var dispatch = (IWorkflowDispatchIndex)store;

        // A plainly-paused run (Suspended, no marker) is NOT claimable — an interactive debug pause stays put.
        await store.SaveAsync("paused", Bytes(), Suspended(resumeRequestedAt: null), WorkflowEtag.None, default);

        // A Suspended run the control plane marked resume-claimable IS claimable (and stays Suspended, never Pending).
        await store.SaveAsync("resume-suspended", Bytes(), Suspended(resumeRequestedAt: T0), WorkflowEtag.None, default);

        // A Faulted run whose checkpoint a caller mutated then marked resume-claimable IS claimable.
        await store.SaveAsync("resume-faulted", Bytes(), Faulted(resumeRequestedAt: T0), WorkflowEtag.None, default);

        List<string> claimable = (await Collect(dispatch.QueryClaimableAsync(["wf"], T0, default))).Select(r => r.Value).ToList();
        claimable.ShouldContain("resume-suspended");
        claimable.ShouldContain("resume-faulted");
        claimable.ShouldNotContain("paused");
    }

    /// <summary>Asserts the resume-claimable path honours the same hosted-workflow-id and environment (§5.5) filters
    /// as fresh dispatch.</summary>
    /// <param name="store">The store under test (must implement <see cref="IWorkflowDispatchIndex"/>).</param>
    /// <returns>A task that completes when the assertions pass.</returns>
    public static async Task Respects_hosted_and_environment_filters(IWorkflowStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var dispatch = (IWorkflowDispatchIndex)store;

        await store.SaveAsync("prod", Bytes(), Suspended(resumeRequestedAt: T0, environment: "production"), WorkflowEtag.None, default);
        await store.SaveAsync("staging", Bytes(), Suspended(resumeRequestedAt: T0, environment: "staging"), WorkflowEtag.None, default);
        await store.SaveAsync("other-wf", Bytes(), new WorkflowRunIndexEntry("other", WorkflowRunStatus.Suspended, T0, T0, ResumeRequestedAt: T0), WorkflowEtag.None, default);

        // §5.5 pinning holds for the resume-claimable path: a production runner claims only the production-pinned run,
        // and never a run for a workflow it does not host.
        List<string> production = (await Collect(dispatch.QueryClaimableAsync(["wf"], "production", T0, default))).Select(r => r.Value).ToList();
        production.ShouldContain("prod");
        production.ShouldNotContain("staging");
        production.ShouldNotContain("other-wf");

        // An unscoped dispatcher (null environment) still filters by hosted id, but claims regardless of environment.
        List<string> unscoped = (await Collect(dispatch.QueryClaimableAsync(["wf"], null, T0, default))).Select(r => r.Value).ToList();
        unscoped.ShouldContain("prod");
        unscoped.ShouldContain("staging");
        unscoped.ShouldNotContain("other-wf");
    }

    private static WorkflowRunIndexEntry Suspended(DateTimeOffset? resumeRequestedAt, string? environment = null)
        => new("wf", WorkflowRunStatus.Suspended, T0, T0, Environment: environment, ResumeRequestedAt: resumeRequestedAt);

    private static WorkflowRunIndexEntry Faulted(DateTimeOffset? resumeRequestedAt)
        => new("wf", WorkflowRunStatus.Faulted, T0, T0, ErrorType: "boom", ResumeRequestedAt: resumeRequestedAt);

    private static byte[] Bytes() => "{}"u8.ToArray();

    private static async Task<List<WorkflowRunId>> Collect(IAsyncEnumerable<WorkflowRunId> source)
    {
        var ids = new List<WorkflowRunId>();
        await foreach (WorkflowRunId id in source)
        {
            ids.Add(id);
        }

        return ids;
    }
}