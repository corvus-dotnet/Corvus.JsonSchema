// <copyright file="RedisEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a deployment
/// environment (design §5.5) persisted for a distributed host. Each authorization is stored verbatim as its
/// <see cref="EnvironmentRunnerAuthorization"/> Corvus.Text.Json document under a per-record key composed from
/// <c>(environment, runnerId)</c>; the filterable fields (status, environment) and the etag travel inside the document, so the
/// unpaged <see cref="ListAsync(RunnerAuthorizationQuery, CancellationToken)"/> materialises the matching records and orders
/// them client-side, while the paged seam walks a keyset index. Because receiving an environment's runs means receiving its
/// credentials, a runner cannot self-assert into an environment — it enters the <c>Pending</c> state and is not dispatchable
/// until an administrator of that environment (§15.1) authorizes it. Mirrors <see cref="RedisAvailabilityRequestStore"/>,
/// keyed by environment + runner rather than a single id.
/// </summary>
/// <remarks>
/// <para>A keyset index gives native <c>(environment, runnerId)</c>-ordered paging: a single zero-scored sorted set whose
/// members are <c>"{environment}\0{runnerId}"</c>, so a <c>ZRANGEBYLEX</c> returns them in ordinal <c>(environment,
/// runnerId)</c> order — the same total order the in-memory pager uses, and the same bytes the continuation token carries. The
/// members are the small key tuples (no documents), so paging seeks strictly past the cursor and fetches only the page's
/// documents lazily — never the whole set.</para>
/// <para>Reads use a pooled <see cref="Lease{T}"/> (no GC read array); a returned document copies the lease span into a
/// pooled document the caller owns. The etag travels inside the document, so optimistic concurrency is a read-compare-write.
/// Targets a single Redis instance (or a primary): an ensure-pending touches the per-record key and the index set, which is
/// not Redis-Cluster slot-safe.</para>
/// </remarks>
public sealed class RedisEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private const string RecordPrefix = "arazzo:runnerauth:";

    // The keyset index for native (environment, runnerId)-ordered paging: a single zero-scored sorted set whose members are
    // "{environment}\0{runnerId}", so a ZRANGEBYLEX returns them in ordinal (environment, runnerId) order — the contract's
    // order and the same bytes the continuation token decodes to. The unpaged ListAsync(query) also enumerates this set to
    // discover every record's key.
    private const string IndexKey = "arazzo:runnerauths:bykey";

    // The null control byte cannot appear in an environment or a runner id, so it separates the two key parts in both the
    // index member and the continuation token (which carries the same bytes).
    private const char MemberSeparator = '\0';

    // Singleton comparer (created once) for the client-side snapshot ordering, since the index set is unordered until paged:
    // by environment (ordinal) then runner id (ordinal).
    private static readonly IComparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>> ByEnvironmentThenRunner =
        Comparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>>.Create(static (a, b) =>
        {
            int c = string.CompareOrdinal(a.RootElement.EnvironmentValue, b.RootElement.EnvironmentValue);
            return c != 0 ? c : string.CompareOrdinal(a.RootElement.RunnerIdValue, b.RootElement.RunnerIdValue);
        });

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisEnvironmentRunnerAuthorizationStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens a runner-authorization store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisEnvironmentRunnerAuthorizationStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisEnvironmentRunnerAuthorizationStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a runner-authorization store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisEnvironmentRunnerAuthorizationStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisEnvironmentRunnerAuthorizationStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending). Under the single-instance assumption a GET-then-SET is acceptable (a Lua
        // check-and-set would be the cluster-safe form). Read into a pooled lease (no GC read array); the returned document
        // copies the lease span into a pooled document the caller owns.
        using (Lease<byte>? existing = await this.database.StringGetLeaseAsync(RecordKey(environment, runnerId)).ConfigureAwait(false))
        {
            if (existing is { Length: > 0 })
            {
                return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing.Span);
            }
        }

        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.database.StringSetAsync(RecordKey(environment, runnerId), json).ConfigureAwait(false);
        await this.database.SortedSetAddAsync(IndexKey, IndexMember(environment, runnerId), 0).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RecordKey(environment, runnerId)).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        try
        {
            // The index members are the small key tuples (no documents), so enumerating them never fetches a body until a
            // candidate matches. Each kept document is read into a pooled lease and copied into an owned pooled document.
            foreach (RedisValue member in await this.database.SortedSetRangeByValueAsync(IndexKey).ConfigureAwait(false))
            {
                if (!TrySplitIndexMember((string)member!, out string memberEnvironment, out string memberRunnerId))
                {
                    continue; // a corrupt member; skip it
                }

                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RecordKey(memberEnvironment, memberRunnerId)).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue; // the index member outlived its record (a concurrent change); skip it
                }

                ParsedJsonDocument<EnvironmentRunnerAuthorization> document = PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(lease.Span);
                if (Matches(document.RootElement, query))
                {
                    list.Add(document);
                }
                else
                {
                    document.Dispose();
                }
            }

            list.Sort(ByEnvironmentThenRunner);
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor to the index member the previous page ended at ("{environment}\0{runnerId}"); the parts
        // reify to a string only here (the leaf). Undefined token = first page; a malformed token throws FormatException.
        string? cursorMember = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> cursorEnvUtf8, out ReadOnlySpan<byte> cursorRunnerUtf8))
                {
                    cursorMember = IndexMemberOf(Encoding.UTF8.GetString(cursorEnvUtf8), Encoding.UTF8.GetString(cursorRunnerUtf8));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // ZRANGEBYLEX returns the members in ordinal (environment, runnerId) order (small strings, no docs); only the page's
        // matching documents are then fetched. The status/environment/administered-set filters live in / derive from the
        // document, so we walk the order and fetch + filter until the page fills (reads ~ pageSize / selectivity), never
        // every record's document.
        RedisValue[] members = await this.database.SortedSetRangeByValueAsync(IndexKey).ConfigureAwait(false);
        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            bool hasMore = false;
            foreach (RedisValue value in members)
            {
                string member = (string)value!;
                if (cursorMember is not null && string.CompareOrdinal(member, cursorMember) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (!TrySplitIndexMember(member, out string memberEnvironment, out string memberRunnerId))
                {
                    continue; // a corrupt member; skip it
                }

                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RecordKey(memberEnvironment, memberRunnerId)).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<EnvironmentRunnerAuthorization> document = PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(lease.Span);
                if (!Matches(document.RootElement, query))
                {
                    document.Dispose();
                    continue;
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // one matching row beyond the page → a next page exists
                    document.Dispose();
                    break;
                }

                page.Add(document);
            }

            if (!hasMore)
            {
                return EnvironmentRunnerAuthorizationPage.Create(page);
            }

            EnvironmentRunnerAuthorization last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastEnv = last.Environment.GetUtf8String();
            using UnescapedUtf8JsonString lastRunner = last.RunnerId.GetUtf8String();
            return EnvironmentRunnerAuthorizationPage.Create(page, lastEnv.Span, lastRunner.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING over the lease (the
        // lease stays alive through the synchronous etag check + merge). A stale etag throws RunnerAuthorizationConflictException.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RecordKey(environment, runnerId)).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(lease.Memory);
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // A decision never changes (environment, runnerId), so the index member is unchanged — only the record body is
        // rewritten.
        await this.database.StringSetAsync(RecordKey(environment, runnerId), json).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
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

    // Per-record value key: holds the authorization document bytes verbatim, composed from (environment, runnerId).
    private static RedisKey RecordKey(string environment, string runnerId)
        => RecordPrefix + environment + MemberSeparator + runnerId;

    // The keyset index member for an authorization: "{environment}\0{runnerId}", so an ordinal (ZRANGEBYLEX) compare of
    // members is (environment asc, runnerId asc) — the in-memory order and the continuation token's bytes.
    private static RedisValue IndexMember(string environment, string runnerId)
        => IndexMemberOf(environment, runnerId);

    private static string IndexMemberOf(string environment, string runnerId)
        => environment + MemberSeparator + runnerId;

    // Splits "{environment}\0{runnerId}" back into its two parts; the inverse of IndexMemberOf. A member without the
    // separator (a corrupt member) is skipped by returning false.
    private static bool TrySplitIndexMember(string member, out string environment, out string runnerId)
    {
        int sep = member.IndexOf(MemberSeparator);
        if (sep < 0)
        {
            environment = string.Empty;
            runnerId = string.Empty;
            return false;
        }

        environment = member[..sep];
        runnerId = member[(sep + 1)..];
        return true;
    }

    private static bool Matches(in EnvironmentRunnerAuthorization authorization, RunnerAuthorizationQuery query)
    {
        // Status + environment are compared string-free (no field is realised to a managed string per row).
        if (query.Status is { } status && !authorization.HasStatus(status))
        {
            return false;
        }

        if (query.Environment is { } environment && !authorization.EnvironmentEquals(environment))
        {
            return false;
        }

        // The approver inbox (§5.5/§7.8): the row's environment must be one the caller administers (server-derived set).
        return query.MatchesAdministeredSet(authorization);
    }
}