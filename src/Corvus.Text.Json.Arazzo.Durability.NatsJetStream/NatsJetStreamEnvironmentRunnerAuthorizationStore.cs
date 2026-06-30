// <copyright file="NatsJetStreamEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a
/// deployment environment (design §5.5) persisted in a single KV bucket. Each authorization is stored as its
/// <see cref="EnvironmentRunnerAuthorization"/> schema document under a key encoding its <c>(environment, runnerId)</c>
/// composite key; the key alone recovers the ordering tuple, so the unordered KV listing is materialised in process,
/// filtered, ordered by <c>(environment, runnerId)</c>, and keyset-paged — exactly as
/// <see cref="NatsJetStreamAvailabilityStore"/> does. The etag travels inside the document (not the KV revision), so
/// optimistic concurrency is a read-compare-write driven by
/// <see cref="EnvironmentRunnerAuthorizationSerialization.SerializeDecision"/>, exactly as the SQLite and in-memory
/// backends do.
/// </summary>
/// <remarks>
/// Each KV key is <c>auth.{base64url(environment)}.{base64url(runnerId)}</c>: Base64Url over the UTF-8 of each part yields
/// only the restricted set of characters a NATS KV key permits, so dots and slashes in an environment name or runner id
/// round-trip safely. Both parts are required (non-empty) here, so the empty-token sentinel never fires; the same Enc/Dec
/// helper as the sibling availability store is used so the encoding is identical.
/// </remarks>
public sealed class NatsJetStreamEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_runner_authorizations";
    private const string KeyPrefix = "auth.";

    // The KV key listing is unordered; the contract is by (environment, runnerId), so the materialised list is sorted by
    // this comparer (mirroring the SQLite ORDER BY Environment, RunnerId).
    private static readonly IComparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>> ByEnvironmentThenRunnerId =
        Comparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>>.Create(static (a, b) =>
        {
            // String-free ordinal compare over the JSON values' UTF-8 (a view for unescaped values, so no per-comparison
            // string is realised); byte order also matches the SQL backends' COLLATE "C"/binary ordering. (The string
            // Compare helper below is retained for the keyset paths, which already hold the parts as strings from the key.)
            using UnescapedUtf8JsonString ae = a.RootElement.Environment.GetUtf8String();
            using UnescapedUtf8JsonString be = b.RootElement.Environment.GetUtf8String();
            int c = ae.Span.SequenceCompareTo(be.Span);
            if (c != 0)
            {
                return c;
            }

            using UnescapedUtf8JsonString ar = a.RootElement.RunnerId.GetUtf8String();
            using UnescapedUtf8JsonString br = b.RootElement.RunnerId.GetUtf8String();
            return ar.Span.SequenceCompareTo(br.Span);
        });

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamEnvironmentRunnerAuthorizationStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the runner-authorization KV bucket (requires stream-management rights); run once at deploy time.</summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the runner-authorization KV bucket over a caller-supplied connection.</summary>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned KV bucket.</summary>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamEnvironmentRunnerAuthorizationStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamEnvironmentRunnerAuthorizationStore(connection, store, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the store for operation over a caller-supplied connection (the caller retains ownership).</summary>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamEnvironmentRunnerAuthorizationStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamEnvironmentRunnerAuthorizationStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        string key = Key(environment, runnerId);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending).
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is { Value: { } bytes })
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(bytes);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(environment, runnerId), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(bytes) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            ParsedJsonDocument<EnvironmentRunnerAuthorization> document = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(bytes.AsMemory());
            if (Matches(document.RootElement, query))
            {
                list.Add(document);
            }
            else
            {
                document.Dispose();
            }
        }

        list.Sort(ByEnvironmentThenRunnerId);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to strings (the leaf) only here. Undefined token = first
        // page; a malformed token throws FormatException.
        string? cursorEnvironment = null;
        string? cursorRunnerId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> cursorEnvUtf8, out ReadOnlySpan<byte> cursorRunnerUtf8))
                {
                    cursorEnvironment = Encoding.UTF8.GetString(cursorEnvUtf8);
                    cursorRunnerId = Encoding.UTF8.GetString(cursorRunnerUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // KV listing is unordered and has no server-side range query, so the keys are enumerated and decoded to their
        // (environment, runnerId) tuple from the key alone; the rows are then ordered by (environment, runnerId).
        var rows = new List<(string Environment, string RunnerId, string Key)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (TryParseKey(key, out (string Environment, string RunnerId) parts))
            {
                rows.Add((parts.Environment, parts.RunnerId, key));
            }
        }

        rows.Sort(static (x, y) => Compare(x.Environment, x.RunnerId, y.Environment, y.RunnerId));

        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            bool hasMore = false;
            foreach ((string environment, string runnerId, string key) in rows)
            {
                if (cursorEnvironment is not null && Compare(environment, runnerId, cursorEnvironment, cursorRunnerId!) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<EnvironmentRunnerAuthorization> document = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(bytes.AsMemory());
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

        string key = Key(environment, runnerId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(bytes.AsMemory());
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The total order: environment (ordinal), then runner id (ordinal) — identical to the SQLite ORDER BY Environment,
    // RunnerId and the in-memory pager's span compare.
    private static int Compare(string e1, string r1, string e2, string r2)
    {
        int c = string.CompareOrdinal(e1, e2);
        return c != 0 ? c : string.CompareOrdinal(r1, r2);
    }

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The inverse of Enc: recovers a key segment's original text. Only ever applied to segments this store wrote.
    private static string Dec(string segment) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

    // The KV key for a single authorization: the namespace, then Base64Url(environment), Base64Url(runnerId), dot-separated.
    // Base64Url emits only [A-Za-z0-9_-], all valid KV key characters, so an environment name or runner id containing dots,
    // slashes, etc. round-trips safely.
    private static string Key(string environment, string runnerId)
        => string.Concat(KeyPrefix, Enc(environment), ".", Enc(runnerId));

    // Inverts Key: splits auth.{Enc(environment)}.{Enc(runnerId)} on its two dot-delimited parts and Base64Url-decodes them
    // back to their original strings, so the ordering tuple is recovered from the key alone.
    private static bool TryParseKey(string key, out (string Environment, string RunnerId) parts)
    {
        parts = default;
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> body = key.AsSpan(KeyPrefix.Length);
        int dot = body.IndexOf('.');
        if (dot < 0)
        {
            return false;
        }

        ReadOnlySpan<char> environmentPart = body[..dot];
        ReadOnlySpan<char> runnerPart = body[(dot + 1)..];
        if (runnerPart.IndexOf('.') >= 0)
        {
            return false;
        }

        try
        {
            parts = (Dec(environmentPart.ToString()), Dec(runnerPart.ToString()));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // The list filter: each absent criterion matches anything, mirroring the SQLite WHERE clause. Status + environment are
    // compared string-free (no field is realised to a managed string per row).
    private static bool Matches(in EnvironmentRunnerAuthorization authorization, RunnerAuthorizationQuery query)
    {
        if (query.Status is { } status && !authorization.HasStatus(status))
        {
            return false;
        }

        if (query.Environment is { } environment && !authorization.EnvironmentEquals(environment))
        {
            return false;
        }

        // The approver inbox (§5.5/§7.8): the authorization's environment must be one the caller administers (server-derived set).
        return query.MatchesAdministeredSet(authorization);
    }

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.store.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }
}