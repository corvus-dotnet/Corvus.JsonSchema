// <copyright file="NatsJetStreamAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow
/// versions are available in which environments) persisted in a single KV bucket. Each entry is stored as its
/// <see cref="AvailabilityEntry"/> document under a key encoding (BaseWorkflowId, VersionNumber, Environment), its presence
/// meaning available. AvailabilityEntry has no mutable state and carries no security tags — an entry is created (idempotently)
/// to make a version available and deleted to withdraw it; authorization and readiness are the control-plane surface's concern.
/// </summary>
/// <remarks>
/// <para>Each KV key is <c>avail.{base64url(baseWorkflowId)}.{versionNumber}.{base64url(environment)}</c>: Base64Url over
/// the UTF-8 of each id component yields only the restricted set of characters a NATS KV key permits, so dots and slashes in
/// ids round-trip, while the integer version number is already key-safe. All three parts are non-empty here (baseWorkflowId
/// and environment are required and the version number is ≥ 1), so the empty-token sentinel never fires; the same Enc/Dec
/// helper as the source store is used so the encoding is identical.</para>
/// <para>The KV store has no server-side ordering or range query, so both list axes are materialised in process: the keys
/// are enumerated, each decoded back to <c>(baseWorkflowId, versionNumber, environment)</c>, filtered to the axis, sorted,
/// and keyset-paged in memory (mirroring <see cref="InMemoryAvailabilityStore"/>) — the only correct option for a key/value
/// backend.</para>
/// </remarks>
public sealed class NatsJetStreamAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_availability";
    private const string KeyPrefix = "avail.";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamAvailabilityStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the availability KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the availability KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamAvailabilityStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamAvailabilityStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamAvailabilityStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamAvailabilityStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);

        string key = Key(baseWorkflowId, versionNumber, environment);

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is { Value: { } bytes })
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(bytes), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId, versionNumber, environment), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? PersistedJson.ToPooledDocument<AvailabilityEntry>(bytes) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        return await this.DeleteKeyAsync(Key(baseWorkflowId, versionNumber, environment), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // KV listing is unordered and has no server-side range query, so the keys are enumerated and decoded to their
        // (baseWorkflowId, versionNumber, environment) tuple from the key alone; the rows for this exact version are kept
        // and ordered by environment (the only varying key part on this axis).
        var rows = new List<(string BaseWorkflowId, int VersionNumber, string Environment, string Key)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!TryParseKey(key, out (string BaseWorkflowId, int VersionNumber, string Environment) parts))
            {
                continue;
            }

            if (string.Equals(parts.BaseWorkflowId, baseWorkflowId, StringComparison.Ordinal) && parts.VersionNumber == versionNumber)
            {
                rows.Add((parts.BaseWorkflowId, parts.VersionNumber, parts.Environment, key));
            }
        }

        rows.Sort(static (x, y) => string.CompareOrdinal(x.Environment, y.Environment));
        return await this.BuildPageAsync(
            rows,
            pageSize,
            hasCursor,
            static (row, cursor) => string.CompareOrdinal(row.Environment, cursor.Environment),
            cursor,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // As above: enumerate and decode the keys, keep the rows for this environment, and order by base workflow id then
        // version number.
        var rows = new List<(string BaseWorkflowId, int VersionNumber, string Environment, string Key)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!TryParseKey(key, out (string BaseWorkflowId, int VersionNumber, string Environment) parts))
            {
                continue;
            }

            if (string.Equals(parts.Environment, environment, StringComparison.Ordinal))
            {
                rows.Add((parts.BaseWorkflowId, parts.VersionNumber, parts.Environment, key));
            }
        }

        rows.Sort(static (x, y) => CompareWorkflowVersion(x.BaseWorkflowId, x.VersionNumber, y.BaseWorkflowId, y.VersionNumber));
        return await this.BuildPageAsync(
            rows,
            pageSize,
            hasCursor,
            static (row, cursor) => CompareWorkflowVersion(row.BaseWorkflowId, row.VersionNumber, cursor.BaseWorkflowId, cursor.VersionNumber),
            cursor,
            cancellationToken).ConfigureAwait(false);
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

    // The by-environment total order: base workflow id (ordinal), then version number (numeric).
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

    // Base64Url over the UTF-8 bytes yields only [A-Za-z0-9_-] (all valid KV subject-token chars), but it maps the empty
    // string to the empty string — and a NATS subject cannot contain an empty token (it would leave a trailing dot in the
    // key). The id parts here are required (non-empty), so empty never occurs; the "_" sentinel is kept identical to the
    // source store's encoding so the two stores share one Enc/Dec, and Dec inverts it before decoding.
    private static string Enc(string value) => value.Length == 0 ? "_" : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string value) => value == "_" ? string.Empty : Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    // The KV key for a single entry: the namespace, then Base64Url(baseWorkflowId), the version number, Base64Url(environment),
    // dot-separated. Base64Url emits only [A-Za-z0-9_-], all valid KV key characters, so the id components — which may contain
    // dots, slashes, etc. — round-trip safely; the version number is an integer and already key-safe.
    private static string Key(string baseWorkflowId, int versionNumber, string environment)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(baseWorkflowId)}.{versionNumber}.{Enc(environment)}");

    // Inverts Key: splits avail.{Enc(baseWorkflowId)}.{versionNumber}.{Enc(environment)} on its three dot-delimited parts and
    // Base64Url-decodes the id parts back to their original strings, so the ordering tuple is recovered from the key alone.
    private static bool TryParseKey(string key, out (string BaseWorkflowId, int VersionNumber, string Environment) parts)
    {
        parts = default;
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> body = key.AsSpan(KeyPrefix.Length);
        int firstDot = body.IndexOf('.');
        if (firstDot < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest = body[(firstDot + 1)..];
        int secondDot = rest.IndexOf('.');
        if (secondDot < 0)
        {
            return false;
        }

        ReadOnlySpan<char> basePart = body[..firstDot];
        ReadOnlySpan<char> versionPart = rest[..secondDot];
        ReadOnlySpan<char> environmentPart = rest[(secondDot + 1)..];
        if (environmentPart.IndexOf('.') >= 0)
        {
            return false;
        }

        if (!int.TryParse(versionPart, NumberStyles.None, CultureInfo.InvariantCulture, out int versionNumber))
        {
            return false;
        }

        try
        {
            parts = (Dec(basePart.ToString()), versionNumber, Dec(environmentPart.ToString()));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Scans the sorted rows past the cursor (per the axis comparer), takes a page, fetching each kept row's document, and
    // emits a token from the last included row's full key when more rows remain. Each page row is parsed into a pooled
    // document the caller owns.
    private async ValueTask<AvailabilityPage> BuildPageAsync(
        List<(string BaseWorkflowId, int VersionNumber, string Environment, string Key)> sorted,
        int pageSize,
        bool hasCursor,
        Func<(string BaseWorkflowId, int VersionNumber, string Environment), (string BaseWorkflowId, int VersionNumber, string Environment), int> compareToCursor,
        (string BaseWorkflowId, int VersionNumber, string Environment) cursor,
        CancellationToken cancellationToken)
    {
        var docs = new PooledDocumentList<AvailabilityEntry>(Math.Min(pageSize, sorted.Count));
        bool hasMore = false;
        string lastBaseWorkflowId = string.Empty, lastEnvironment = string.Empty;
        int lastVersionNumber = 0;
        try
        {
            foreach ((string BaseWorkflowId, int VersionNumber, string Environment, string Key) row in sorted)
            {
                if (hasCursor && compareToCursor((row.BaseWorkflowId, row.VersionNumber, row.Environment), cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                // Fetch the document lazily — only for keys past the cursor, and only until the page fills plus one.
                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(row.Key, cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // raced with a withdraw — treat as absent
                }

                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(bytes));
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

    private async ValueTask<bool> DeleteKeyAsync(string key, CancellationToken cancellationToken)
    {
        // NATS KV DeleteAsync is idempotent — it writes a delete marker and never signals whether a live value existed —
        // so withdraw's "was it actually there?" answer comes from a prior read, mirroring the sibling source store's
        // DeleteAsync. TryGetAsync returns null for both a never-created key and an already-deleted one (its delete
        // marker surfaces as NatsKVKeyDeletedException), so a second withdraw correctly reports false.
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not { Value: not null })
        {
            return false;
        }

        await this.store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }
}