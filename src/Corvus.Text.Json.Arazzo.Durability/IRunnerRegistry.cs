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
    /// Lists the currently-registered runners. The full read used by hosting/scan paths and by the default keyset pager;
    /// the paged <see cref="ListAsync(int, JsonString, CancellationToken)"/> is the API list seam.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A snapshot of the registered runners. Each value is detached from any store-side buffer.</returns>
    ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists runners as a keyset page: runners ordered by <c>runnerId</c>, bounded to <paramref name="limit"/>, resuming
    /// strictly after <paramref name="pageToken"/>. The default implementation pages over
    /// <see cref="ListAsync(CancellationToken)"/> in memory; a backend overrides it with a native keyset query so the read
    /// itself is bounded.
    /// </summary>
    /// <param name="limit">The maximum runners to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="RunnerRegistryPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<RunnerRegistryPage> ListAsync(int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        IReadOnlyList<RunnerRegistration> all = await this.ListAsync(cancellationToken).ConfigureAwait(false);
        return RunnerRegistryPaging.PageInMemory(all, limit, pageToken);
    }

    /// <summary>
    /// Lists runners as a <b>reach-scoped</b> keyset page (design §5.5/§14.2): the same keyset page as
    /// <see cref="ListAsync(int, JsonString, CancellationToken)"/>, but every runner outside the caller's read reach
    /// (its <c>reachTags</c> not admitted by <paramref name="context"/>) is dropped first, so a tenant sees only the
    /// runners serving its environments. This is the seam the control-plane <c>GET /runners</c> endpoint uses.
    /// </summary>
    /// <param name="context">The caller's resolved row-access grant (<see cref="AccessContext.System"/> for the trusted, full-reach path).</param>
    /// <param name="limit">The maximum runners to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="RunnerRegistryPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>One reach-filtered keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    /// <remarks>The default reach-filters a full read in memory; a backend may override it once it can push the
    /// <c>reachTags</c> predicate into a native query, exactly as the unscoped overload is overridden for paging.</remarks>
    async ValueTask<RunnerRegistryPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<RunnerRegistration> all = await this.ListAsync(cancellationToken).ConfigureAwait(false);
        return RunnerRegistryPaging.PageInMemory(all, context, limit, pageToken);
    }

    /// <summary>
    /// Counts the runners visible to <paramref name="context"/> (design §5.5/§14.2), bounded by <paramref name="cap"/>:
    /// the reach-scoped total behind the console's <c>/runners</c> list footer, without returning any runner rows.
    /// </summary>
    /// <param name="context">The caller's resolved row-access grant (<see cref="AccessContext.System"/> for the trusted, full-reach path); a runner outside its read reach is not counted.</param>
    /// <param name="cap">The inclusive upper bound on the reported count; once <paramref name="cap"/> reach-visible runners have been seen the count saturates and <c>Capped</c> is <see langword="true"/> (a non-positive value uses the store's default page size as the cap).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The bounded count and whether it was capped (the true total meets or exceeds <paramref name="cap"/>).</returns>
    /// <remarks>
    /// Reach here is a per-row ABAC predicate over each runner's <c>reachTags</c>, <b>not</b> a SQL-expressible filter
    /// (unlike the run/catalog reach side-tables), so a native <c>COUNT</c> is impossible: the reach-scoped
    /// <see cref="ListAsync(AccessContext, int, JsonString, CancellationToken)"/> already <i>is</i> the bounded native read —
    /// it streams the ordered registry and reach-filters in flight, stopping once the page fills. This default asks it for a
    /// single page of <paramref name="cap"/> + 1 and reports the bounded count, so every backend counts through its own
    /// native reach query with no separate count path to keep in sync with the list's predicate.
    /// </remarks>
    async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int bound = cap > 0 ? cap : RunnerRegistryPage.DefaultPageSize;
        using RunnerRegistryPage page = await this.ListAsync(context, bound + 1, default, cancellationToken).ConfigureAwait(false);
        int visible = page.Runners.Count;
        return visible > bound ? (bound, true) : (visible, false);
    }

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
    /// Determines whether any registered runner serving <paramref name="environment"/> declares that it hosts §18
    /// draft runs (<see cref="RunnerRegistration.HostsDraftRunsValue"/>) — the draft analogue of
    /// <see cref="IsVersionHostedAsync"/>, and the predicate behind the control plane's fail-closed
    /// <c>no-runner</c> gate on a debug-run start (workflow-designer design §18). Environment-scoped because
    /// draft dispatch is environment-pinned: a draft-hosting runner in another environment cannot claim the run.
    /// </summary>
    /// <param name="environment">The environment the draft run targets.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if a registered runner serving the environment hosts draft runs.</returns>
    /// <remarks>
    /// The default scans <see cref="ListAsync(CancellationToken)"/> — the documented full-read hosting/scan seam,
    /// bounded by the number of registered runner processes (unlike the per-version hosting index, which scales
    /// with runners × hosted versions and sits on the hot trigger path). A backend may override it with a native
    /// indexed query if a deployment's registry grows past that.
    /// </remarks>
    async ValueTask<bool> IsDraftRunsHostedAsync(string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);

        IReadOnlyList<RunnerRegistration> runners = await this.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (RunnerRegistration runner in runners)
        {
            if (runner.HostsDraftRunsValue && ((JsonElement)runner.Environment).EqualsString(environment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes every runner whose last heartbeat is strictly older than <paramref name="deadBefore"/>.
    /// </summary>
    /// <param name="deadBefore">Runners last seen before this instant are considered dead and pruned.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of runners removed.</returns>
    ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken);
}