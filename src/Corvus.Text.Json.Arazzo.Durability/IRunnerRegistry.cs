// <copyright file="IRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A directory of the live runner processes that can execute hosted workflows. A runner registers
/// itself on start-up, heartbeats while it is alive, and is pruned once its heartbeat goes stale.
/// </summary>
/// <remarks>
/// The registry persists each <see cref="RunnerRegistration"/> as its JSON document — the store keeps the
/// runner's record verbatim rather than decomposing it into columns, so the same shape round-trips through
/// every backend. The control plane reads the registry to answer <c>GET /runners</c> and to decide whether a
/// trigger can be accepted (there must be a live runner that hosts the targeted version).
/// </remarks>
public interface IRunnerRegistry
{
    /// <summary>
    /// Registers (or replaces) a runner. The registration's <see cref="RunnerRegistration.RunnerId"/> is the key.
    /// </summary>
    /// <param name="registration">The runner registration to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the registration has been persisted.</returns>
    ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken);

    /// <summary>
    /// Records a liveness heartbeat for a registered runner, advancing its <see cref="RunnerRegistration.LastSeenAt"/>.
    /// </summary>
    /// <param name="runnerId">The id of the runner sending the heartbeat.</param>
    /// <param name="at">The instant of the heartbeat.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if a registration existed and was updated; <see langword="false"/> if the runner
    /// is unknown (for example it was already pruned and must register again).
    /// </returns>
    ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the currently-registered runners.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A snapshot of the registered runners. Each value is detached from any store-side buffer.</returns>
    ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether any registered runner currently hosts (with the version loaded) the given catalog version.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id of the version.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if at least one registered runner advertises this version in its hosted versions
    /// with <c>loaded == true</c>. Backends answer this from an index, not by scanning every runner.
    /// </returns>
    ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every runner whose last heartbeat is strictly older than <paramref name="deadBefore"/>.
    /// </summary>
    /// <param name="deadBefore">Runners last seen before this instant are considered dead and pruned.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of runners removed.</returns>
    ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken);
}