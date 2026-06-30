// <copyright file="RedisRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored verbatim as its
/// JSON document under a per-runner key <c>arazzo:runner:{runnerId}</c>, and a single set <c>arazzo:runners</c>
/// holds the runner ids so the registry can be enumerated — the same authoritative-store-with-index pattern
/// <see cref="RedisWorkflowCatalogStore"/> uses for catalog versions.
/// </summary>
/// <remarks>
/// A secondary index answers <see cref="IsVersionHostedAsync"/> without scanning every runner: a set
/// <c>arazzo:hosting:{enc(baseWorkflowId)}:{versionNumber}</c> holds the ids of runners that host that version with
/// the version loaded. <c>RegisterAsync</c> re-projects the index for a runner (dropping the memberships implied by
/// its previous registration before adding its new ones) and <c>PruneAsync</c> removes a stale runner's memberships.
/// </remarks>
/// <remarks>
/// Targets a single Redis instance (or a primary): registering and pruning touch a per-runner key, the
/// shared index set, and the per-version hosting sets, which is not Redis-Cluster slot-safe. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> (or <see cref="Connect(IConnectionMultiplexer)"/>).
/// </remarks>
public sealed class RedisRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string Prefix = "arazzo:runner:";
    private const string IndexKey = "arazzo:runners";
    private const string HostingPrefix = "arazzo:hosting:";

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly bool ownsConnection;

    private RedisRunnerRegistry(IConnectionMultiplexer connection, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the registry can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens a runner registry over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisRunnerRegistry> ConnectAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisRunnerRegistry(connection, ownsConnection: true);
    }

    /// <summary>Creates a runner registry over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <returns>The registry.</returns>
    public static RedisRunnerRegistry Connect(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisRunnerRegistry(connection, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string runnerId = registration.RunnerIdValue;

        // Re-project this runner's hosting index. Redis cannot query into the stored JSON doc, so first read the
        // runner's existing doc to learn its OLD loaded hosted versions and drop those memberships, then overwrite
        // the doc and add this runner to a hosting set for each of its NEW loaded versions.
        RedisValue existing = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
        if (!existing.IsNullOrEmpty)
        {
            RunnerRegistration old = RunnerRegistration.FromJson((byte[])existing!);
            foreach ((string baseWorkflowId, int versionNumber) in old.LoadedHostedVersions())
            {
                await this.database.SetRemoveAsync(HostingKey(baseWorkflowId, versionNumber), runnerId).ConfigureAwait(false);
            }
        }

        byte[] doc = PersistedJson.ToArray(registration, static (Utf8JsonWriter writer, in RunnerRegistration r) => r.WriteTo(writer));

        await this.database.StringSetAsync(RunnerKey(runnerId), doc).ConfigureAwait(false);
        await this.database.SetAddAsync(IndexKey, runnerId).ConfigureAwait(false);

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await this.database.SetAddAsync(HostingKey(baseWorkflowId, versionNumber), runnerId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();
        return await this.database.SetLengthAsync(HostingKey(baseWorkflowId, versionNumber)).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        cancellationToken.ThrowIfCancellationRequested();

        RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return false;
        }

        byte[] existing = (byte[])value!;
        byte[] doc = PersistedJson.ToArray((existing, at), static (Utf8JsonWriter writer, in (byte[] Existing, DateTimeOffset At) ctx) =>
        {
            using ParsedJsonDocument<RunnerRegistration> parsed = ParsedJsonDocument<RunnerRegistration>.Parse(ctx.Existing);
            parsed.RootElement.WriteWithLastSeenAt(writer, ctx.At);
        });

        await this.database.StringSetAsync(RunnerKey(runnerId), doc).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] runnerIds = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        var result = new List<RunnerRegistration>();
        foreach (RedisValue member in runnerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value = await this.database.StringGetAsync(RunnerKey((string)member!)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            result.Add(RunnerRegistration.FromJson((byte[])value!));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;

        // Decode the keyset cursor; the runner id reifies to a string only for the ordinal compare + key lookup (the leaf).
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        // A Redis set is unordered, so — exactly like the runs store — the keyset order is materialised client-side over the
        // id index (small strings, ordinal == the in-memory pager's order); only the page's documents are then fetched
        // (one beyond, to look ahead), never every registration's JSON.
        RedisValue[] members = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        var ids = new List<string>(members.Length);
        foreach (RedisValue member in members)
        {
            ids.Add((string)member!);
        }

        ids.Sort(static (a, b) => string.CompareOrdinal(a, b));

        var page = new List<RunnerRegistration>(pageSize + 1);
        foreach (string runnerId in ids)
        {
            if (after is not null && string.CompareOrdinal(runnerId, after) <= 0)
            {
                continue; // already returned in an earlier page
            }

            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue; // id indexed but doc gone (raced with prune) — skip
            }

            page.Add(RunnerRegistration.FromJson((byte[])value!));
            if (page.Count > pageSize)
            {
                break; // fetched one real row beyond the page → a next page exists
            }
        }

        if (page.Count <= pageSize)
        {
            return RunnerRegistryPage.Create(page);
        }

        page.RemoveAt(page.Count - 1);
        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.ReadReach is null)
        {
            // Unrestricted read reach (e.g. the trusted system path): no row is filtered, so the look-ahead-bounded
            // keyset walk (one page of documents fetched, no per-row tag work) is exactly right — no full fleet read.
            return await this.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);
        }

        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;

        // Decode the keyset cursor; the runner id reifies to a string only for the ordinal compare + key lookup (the leaf).
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        // Reach (§14.2) is a per-row ABAC predicate, not something Redis can evaluate, so — exactly like the unscoped
        // overload — the keyset order is materialised client-side over the id index (small strings, ordinal == the
        // in-memory pager's order). Documents are then fetched one at a time in that order, seeking past the cursor and
        // reach-filtering in flight, until the page fills with a one-row look-ahead — never the full fleet the in-memory
        // fallback reads. (No fetch limit: filtered-out rows don't count toward the page, so the page-fill governs the walk.)
        RedisValue[] members = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        var ids = new List<string>(members.Length);
        foreach (RedisValue member in members)
        {
            ids.Add((string)member!);
        }

        ids.Sort(static (a, b) => string.CompareOrdinal(a, b));

        var page = new List<RunnerRegistration>(pageSize);
        bool hasMore = false;
        foreach (string runnerId in ids)
        {
            if (after is not null && string.CompareOrdinal(runnerId, after) <= 0)
            {
                continue; // already returned in an earlier page
            }

            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue; // id indexed but doc gone (raced with prune) — skip
            }

            RunnerRegistration runner = RunnerRegistration.FromJson((byte[])value!);

            // reachTags is absent on a runner serving an unscoped environment; an empty tag set fails a scoped reach
            // (fail-closed), so such a runner is invisible to a tenant-scoped caller — matching the in-memory pager.
            SecurityTagSet tags = runner.ReachTags.IsNotUndefined()
                ? SecurityTagSet.CopyFrom(runner.ReachTags)
                : SecurityTagSet.Empty;
            if (!context.Admits(AccessVerb.Read, tags))
            {
                continue; // FromJson allocates a managed registration, not a pooled lease — nothing to dispose for a skip
            }

            if (page.Count == pageSize)
            {
                hasMore = true; // a further reach-visible row exists → there is a next page after the last included row
                break;
            }

            page.Add(runner);
        }

        if (!hasMore)
        {
            return RunnerRegistryPage.Create(page);
        }

        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue[] runnerIds = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        int removed = 0;
        foreach (RedisValue member in runnerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string runnerId = (string)member!;
            RedisValue value = await this.database.StringGetAsync(RunnerKey(runnerId)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            RunnerRegistration registration = RunnerRegistration.FromJson((byte[])value!);
            if (registration.LastSeenAtValue < deadBefore)
            {
                // Remove this runner's hosting memberships (derived from its stored doc) before deleting it.
                foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
                {
                    await this.database.SetRemoveAsync(HostingKey(baseWorkflowId, versionNumber), runnerId).ConfigureAwait(false);
                }

                await this.database.KeyDeleteAsync(RunnerKey(runnerId)).ConfigureAwait(false);
                await this.database.SetRemoveAsync(IndexKey, runnerId).ConfigureAwait(false);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Disposes the connection if this registry created it (from a configuration string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey RunnerKey(string runnerId)
        => Prefix + runnerId;

    /// <summary>
    /// Builds the hosting-set key for a (base workflow id, version) pair. The base id is Base64Url-encoded (with the
    /// <c>=</c> padding trimmed) so it never contains a <c>:</c> that would collide with the key's structural
    /// separators.
    /// </summary>
    private static RedisKey HostingKey(string baseWorkflowId, int versionNumber)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(baseWorkflowId)).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        return $"{HostingPrefix}{encoded}:{versionNumber}";
    }
}