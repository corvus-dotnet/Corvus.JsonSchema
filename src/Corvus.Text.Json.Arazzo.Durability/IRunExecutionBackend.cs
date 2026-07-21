// <copyright file="IRunExecutionBackend.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A pluggable execution backend a runner dispatches a run to (ADR 0028). It advances a run (start or resume,
/// uniform) under a chosen <see cref="IsolationModel"/>, and can be asked to pre-warm a version. The in-process
/// collectible-load-context backend (<see cref="HostedWorkflowResumer"/>) is the shipping default; out-of-process
/// backends (serverless, container, micro-guest) plug in behind the same seam without touching dispatch, leases,
/// timers, or message delivery.
/// </summary>
/// <remarks>
/// <see cref="AdvanceAsync"/> is delegate-shaped, so a backend is consumed anywhere a <see cref="WorkflowResumer"/>
/// is: the dispatcher, worker, and management client keep taking the delegate, and a backend slots in behind it
/// (its <c>AsResumer</c> or the <see cref="AdvanceAsync"/> method group directly), so introducing the seam does
/// not disturb the invocation plumbing.
/// </remarks>
public interface IRunExecutionBackend
{
    /// <summary>Gets the isolation this backend runs a run under. A runner advertises it in its registration, and dispatch matches it against a run's required isolation.</summary>
    RunIsolationModel IsolationModel { get; }

    /// <summary>
    /// Advances a run: starts it (fresh) or resumes it (restored checkpoint), returning the tri-state outcome. The
    /// signature matches <see cref="WorkflowResumer"/>, so this method group is the delegate the dispatcher, worker,
    /// and management client consume.
    /// </summary>
    /// <param name="run">The run to start or resume.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    ValueTask<WorkflowRunResultKind> AdvanceAsync(WorkflowRun run, CancellationToken cancellationToken);

    /// <summary>
    /// Pre-warms the backend for a version, so a later <see cref="AdvanceAsync"/> of it avoids the cold cost: the
    /// in-process backend eagerly loads and caches the executor, and an out-of-process backend ensures a warm
    /// instance holds it. A no-op is a valid implementation for a backend with nothing to warm.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the version is warm.</returns>
    ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);
}