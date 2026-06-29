// <copyright file="RedisAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow versions are
/// available in which environments) persisted as <see cref="AvailabilityEntry"/> documents. Each cell is stored verbatim
/// under a per-entry key composed from (BaseWorkflowId, VersionNumber, Environment); availability has no mutable state and
/// carries no security tags — an entry is created (idempotently) to make a version available and deleted to withdraw it,
/// while authorization and readiness are the control-plane surface's concern.
/// </summary>
/// <remarks>
/// <para>Redis has no server-side filtering, ordering, or range queries, so this correctness-first reference keeps a
/// single global index SET whose members are the entries' composite keys. Both list axes enumerate that index in memory:
/// the candidate members are the small key tuples (no documents), so they are parsed, filtered to the axis, sorted into
/// the axis's stable total order, seeked past the cursor, and only then are the page's documents fetched lazily — never
/// the whole matrix.</para>
/// <para>Targets a single Redis instance (or a primary): a make-available touches the per-entry key and the index set,
/// which is not Redis-Cluster slot-safe.</para>
/// </remarks>
public sealed class RedisAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:availability:";

    // Per-entry value key: holds the AvailabilityEntry document bytes verbatim.
    private const string EntryPrefix = Prefix;

    // Global index set: members are the composite keys "{baseWorkflowId}<sep>{versionNumber}<sep>{environment}", so the
    // list axes can enumerate every entry.
    private const string IndexKey = Prefix + "index";

    // A control character that cannot appear in a key part separates the composite-key parts in keys/members; the same
    // separator RedisSourceStore uses to join identity parts.
    private const char Separator = '\u0001';

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisAvailabilityStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens an availability store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisAvailabilityStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisAvailabilityStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates an availability store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisAvailabilityStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisAvailabilityStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged. Under
        // the single-instance assumption a GET-then-SET is acceptable (a Lua check-and-set would be the cluster-safe form).
        RedisValue existing = await this.database.StringGetAsync(EntryKey(baseWorkflowId, versionNumber, environment)).ConfigureAwait(false);
        if (!existing.IsNullOrEmpty)
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>((byte[])existing!), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.database.StringSetAsync(EntryKey(baseWorkflowId, versionNumber, environment), json).ConfigureAwait(false);
        await this.database.SetAddAsync(IndexKey, IndexMember(baseWorkflowId, versionNumber, environment)).ConfigureAwait(false);
        return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.StringGetAsync(EntryKey(baseWorkflowId, versionNumber, environment)).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : PersistedJson.ToPooledDocument<AvailabilityEntry>((byte[])value!);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();
        bool removed = await this.database.KeyDeleteAsync(EntryKey(baseWorkflowId, versionNumber, environment)).ConfigureAwait(false);
        await this.database.SetRemoveAsync(IndexKey, IndexMember(baseWorkflowId, versionNumber, environment)).ConfigureAwait(false);
        return removed;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Keep the entries for this exact (baseWorkflowId, versionNumber), ordered by environment (the only varying key
        // part on this axis).
        List<(string BaseWorkflowId, int VersionNumber, string Environment)> rows = await this.LoadIndexAsync(
            key => string.Equals(key.BaseWorkflowId, baseWorkflowId, StringComparison.Ordinal) && key.VersionNumber == versionNumber).ConfigureAwait(false);
        rows.Sort(static (x, y) => string.CompareOrdinal(x.Environment, y.Environment));
        return await this.BuildPageAsync(
            rows,
            pageSize,
            hasCursor,
            static (row, cursor) => string.CompareOrdinal(row.Environment, cursor.Environment),
            cursor).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Keep the entries for this environment, ordered by base workflow id (ordinal) then version number (numeric).
        List<(string BaseWorkflowId, int VersionNumber, string Environment)> rows = await this.LoadIndexAsync(
            key => string.Equals(key.Environment, environment, StringComparison.Ordinal)).ConfigureAwait(false);
        rows.Sort(static (x, y) => CompareWorkflowVersion(x.BaseWorkflowId, x.VersionNumber, y.BaseWorkflowId, y.VersionNumber));
        return await this.BuildPageAsync(
            rows,
            pageSize,
            hasCursor,
            static (row, cursor) => CompareWorkflowVersion(row.BaseWorkflowId, row.VersionNumber, cursor.BaseWorkflowId, cursor.VersionNumber),
            cursor).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The by-environment total order: base workflow id (ordinal), then version number (numeric). Matches
    // InMemoryAvailabilityStore.CompareWorkflowVersion so the keyset cursor agrees across backends.
    private static int CompareWorkflowVersion(string b1, int v1, string b2, int v2)
    {
        int c = string.CompareOrdinal(b1, b2);
        return c != 0 ? c : v1.CompareTo(v2);
    }

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    private static RedisKey EntryKey(string baseWorkflowId, int versionNumber, string environment)
        => EntryPrefix + baseWorkflowId + Separator + versionNumber.ToString(CultureInfo.InvariantCulture) + Separator + environment;

    private static RedisValue IndexMember(string baseWorkflowId, int versionNumber, string environment)
        => baseWorkflowId + Separator + versionNumber.ToString(CultureInfo.InvariantCulture) + Separator + environment;

    // Splits "{baseWorkflowId}<sep>{versionNumber}<sep>{environment}" back into its three parts; the round-trip inverse of
    // IndexMember. A version part that is not an integer (a corrupt member) is skipped by returning false.
    private static bool TrySplitIndexMember(string member, out (string BaseWorkflowId, int VersionNumber, string Environment) key)
    {
        key = default;
        int firstSep = member.IndexOf(Separator, StringComparison.Ordinal);
        if (firstSep < 0)
        {
            return false;
        }

        int secondSep = member.IndexOf(Separator, firstSep + 1);
        if (secondSep < 0)
        {
            return false;
        }

        if (!int.TryParse(member.AsSpan(firstSep + 1, secondSep - firstSep - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int versionNumber))
        {
            return false;
        }

        key = (member[..firstSep], versionNumber, member[(secondSep + 1)..]);
        return true;
    }

    // Enumerates the global index, parses each composite-key member, and keeps those matching the axis predicate. The
    // members are the small key tuples (no documents), so this never fetches the matrix's bodies.
    private async ValueTask<List<(string BaseWorkflowId, int VersionNumber, string Environment)>> LoadIndexAsync(
        Func<(string BaseWorkflowId, int VersionNumber, string Environment), bool> keep)
    {
        RedisValue[] members = await this.database.SetMembersAsync(IndexKey).ConfigureAwait(false);
        var rows = new List<(string BaseWorkflowId, int VersionNumber, string Environment)>();
        foreach (RedisValue member in members)
        {
            if (TrySplitIndexMember((string)member!, out (string BaseWorkflowId, int VersionNumber, string Environment) key) && keep(key))
            {
                rows.Add(key);
            }
        }

        return rows;
    }

    // Scans the sorted keys past the cursor (per the axis comparer), takes a page, fetching each kept entry's document
    // bytes lazily, and emits a token from the last included row's full key when more rows remain. Each page row is parsed
    // into a pooled document the caller owns.
    private async ValueTask<AvailabilityPage> BuildPageAsync(
        List<(string BaseWorkflowId, int VersionNumber, string Environment)> sorted,
        int pageSize,
        bool hasCursor,
        Func<(string BaseWorkflowId, int VersionNumber, string Environment), (string BaseWorkflowId, int VersionNumber, string Environment), int> compareToCursor,
        (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        var docs = new PooledDocumentList<AvailabilityEntry>(Math.Min(pageSize, sorted.Count));
        bool hasMore = false;
        string lastBaseWorkflowId = string.Empty, lastEnvironment = string.Empty;
        int lastVersionNumber = 0;
        try
        {
            foreach ((string BaseWorkflowId, int VersionNumber, string Environment) row in sorted)
            {
                if (hasCursor && compareToCursor(row, cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                RedisValue value = await this.database.StringGetAsync(EntryKey(row.BaseWorkflowId, row.VersionNumber, row.Environment)).ConfigureAwait(false);
                if (value.IsNullOrEmpty)
                {
                    continue; // the index member outlived its entry (a concurrent withdraw); skip it
                }

                if (docs.Count == pageSize)
                {
                    // A further row beyond the page exists: stop and hand back a cursor at the last included row.
                    hasMore = true;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>((byte[])value!));
                lastBaseWorkflowId = row.BaseWorkflowId;
                lastVersionNumber = row.VersionNumber;
                lastEnvironment = row.Environment;
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }
}