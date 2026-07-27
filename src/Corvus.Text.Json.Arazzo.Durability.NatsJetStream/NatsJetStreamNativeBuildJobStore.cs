// <copyright file="NatsJetStreamNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055) persisted in a single KV
/// bucket. Each job is stored as its <see cref="NativeBuildJob"/> schema document under a namespaced, Base64Url-encoded key.
/// The KV listing is unordered, so the unpaged <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/> materialises
/// every job and filters / orders client-side; the paged seam adds a keyset index of marker keys for oldest-first paging. The
/// id is derived from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent put, and the store-owned etag travels
/// inside the document. Mirrors <see cref="NatsJetStreamAvailabilityRequestStore"/>, keyed by the target-derived id rather
/// than a random id.
/// </summary>
/// <remarks>The claim walks the keyset index oldest-first and transitions the first Queued job to Building under the KV's
/// native revision compare-and-set (<c>UpdateAsync</c> with the read revision); a lost CAS means a concurrent worker claimed
/// it, so the claim retries the next-oldest — two workers never claim the same job.</remarks>
public sealed class NatsJetStreamNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_native_build_jobs";
    private const string JobPrefix = "job.";

    // A keyset index for native oldest-first paging: one marker key per job of the form
    // "idx.{Base64Url(createdAtIso)}.{Base64Url(id)}". KV listing is unordered, so the order is materialised client-side by
    // decoding (createdAt, id) from each index key — enumerating these is cheap (no documents), and only the page's / claim's
    // documents are then fetched. The "idx." prefix never collides with the "job." record keys.
    private const string IndexPrefix = "idx.";

    // The bounded number of oldest-Queued candidates a single claim will race for before giving up; each lost race means a
    // concurrent worker won that candidate, so under real contention a claim succeeds well within this budget.
    private const int MaxClaimAttempts = 64;

    private static readonly byte[] IndexMarker = "1"u8.ToArray();

    // The KV key listing is unordered; the contract is oldest-first by creation time, with the id as a stable tiebreak.
    private static readonly IComparer<ParsedJsonDocument<NativeBuildJob>> ByCreatedAtThenId =
        Comparer<ParsedJsonDocument<NativeBuildJob>>.Create(static (a, b) =>
        {
            int byCreated = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            if (byCreated != 0)
            {
                return byCreated;
            }

            // Tiebreak on id string-free: compare the JSON values' UTF-8 bytes (no id string is realised per compare).
            using UnescapedUtf8JsonString aId = a.RootElement.Id.GetUtf8String();
            using UnescapedUtf8JsonString bId = b.RootElement.Id.GetUtf8String();
            return aId.Span.SequenceCompareTo(bId.Span);
        });

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamNativeBuildJobStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the native-build-job KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the native-build-job KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamNativeBuildJobStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamNativeBuildJobStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamNativeBuildJobStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamNativeBuildJobStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = NativeBuildJobSerialization.SerializeNew(id, draft, now, etag);

        // A re-enqueue resets createdAt (SerializeNew stamps a fresh instant), so retire any stale index marker the prior
        // enqueue left (an index key embeds the createdAt) before writing the new one. Unlike the availability store (whose
        // createdAt is immutable), this store maintains its index across re-enqueues.
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (existing is { Value: { } priorBytes })
        {
            using ParsedJsonDocument<NativeBuildJob> prior = ParsedJsonDocument<NativeBuildJob>.Parse(priorBytes.AsMemory());
            await this.store.DeleteAsync(IndexKey(prior.RootElement.CreatedAtValue, id), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await this.store.PutAsync(JobPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.store.PutAsync(IndexKey(now, id), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory()) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        // One `now` guards the lease-expiry test (which orphaned Building jobs are reclaimable) and stamps the new
        // lease/startedAt, so the claim is evaluated at a single instant.
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Enumerate the index keys (cheap, no documents) and recover (createdAt, id) from each; KV listing is unordered, so
        // the (createdAt, id) order is materialised client-side.
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(IndexPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string rest = key[IndexPrefix.Length..];
            int dot = rest.IndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            keys.Add((Dec(rest[..dot]), Dec(rest[(dot + 1)..])));
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        // Try the oldest CLAIMABLE candidate first (Queued, or an orphaned Building whose lease has expired — ADR 0056),
        // transitioning it to Building under the KV native revision CAS (UpdateAsync with the read revision); on a
        // wrong-revision conflict (a concurrent claim already advanced it) retry the next-oldest, so two workers never claim
        // the same job.
        int attempts = 0;
        foreach ((string _, string id) in keys)
        {
            if (attempts++ >= MaxClaimAttempts)
            {
                break;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue; // indexed but record gone — skip
            }

            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
            if (!current.RootElement.IsClaimable(now))
            {
                continue; // completed, or Building with a live lease
            }

            WorkflowEtag etag = NewEtag();
            byte[] claimed = NativeBuildJobSerialization.SerializeClaimed(current.RootElement, claimedBy, now, now + leaseTtl, etag);
            try
            {
                await this.store.UpdateAsync(JobPrefix + Enc(id), claimed, entry.Value.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
                return PersistedJson.ToPooledDocument<NativeBuildJob>(claimed);
            }
            catch (NatsKVException)
            {
                // Lost the revision CAS (a concurrent claim advanced the record) — retry the next-oldest.
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForCompletion(id);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = NativeBuildJobSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(JobPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        // Parse the existing document NON-COPYING; it must be Building, then the etag is checked (a reclaim would have moved
        // it, so a stale expected etag conflicts) and the lease-extended record put back. The lease rides inside the document.
        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForLeaseRenewal(id);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = NativeBuildJobSerialization.SerializeRenewal(current.RootElement, id, expectedEtag, this.timeProvider.GetUtcNow() + leaseTtl, etag);
        await this.store.PutAsync(JobPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<NativeBuildJob>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(JobPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            ParsedJsonDocument<NativeBuildJob> document = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
            if (query.Matches(document.RootElement))
            {
                list.Add(document);
            }
            else
            {
                document.Dispose();
            }
        }

        list.Sort(ByCreatedAtThenId);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native point read by the derived key (the target tuple maps to a unique record key) — never a scan.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return false;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
        return current.RootElement.HasStatus(NativeBuildJobStatus.Ready);
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to strings (the leaf) only here — createdAt as the ISO-8601 "o"
        // form (reconstructed from the token's UTC ticks). Undefined token = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(NativeBuildJobContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (NativeBuildJobContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Enumerate the index keys (cheap, no documents) and recover (createdAt, id) from each; KV listing is unordered, so
        // the (createdAt, id) order is materialised client-side. Then only the page's documents are fetched + filtered.
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(IndexPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string rest = key[IndexPrefix.Length..];
            int dot = rest.IndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            keys.Add((Dec(rest[..dot]), Dec(rest[(dot + 1)..])));
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        var page = new PooledDocumentList<NativeBuildJob>(pageSize);
        try
        {
            bool hasMore = false;
            foreach ((string createdAt, string id) in keys)
            {
                if (cursorCreatedAt is not null)
                {
                    int byCreated = string.CompareOrdinal(createdAt, cursorCreatedAt);
                    bool after = byCreated > 0 || (byCreated == 0 && string.CompareOrdinal(id, cursorId) > 0);
                    if (!after)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }
                }

                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(JobPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<NativeBuildJob> document = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
                if (!query.Matches(document.RootElement))
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
                return NativeBuildJobPage.Create(page);
            }

            NativeBuildJob last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return NativeBuildJobPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        // Bounded scan: read + parse each candidate only to apply the same Matches predicate as the list, disposing it
        // immediately (no PooledDocumentList grows); stop once a (cap+1)th match is seen and report Capped.
        int count = 0;
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(JobPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            using ParsedJsonDocument<NativeBuildJob> document = ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory());
            if (query.Matches(document.RootElement) && ++count > cap)
            {
                return (cap, true);
            }
        }

        return (count, false);
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

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The inverse of Enc: recovers a key segment's original text. Only ever applied to segments this store wrote.
    private static string Dec(string segment) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

    // The keyset index key for a job: "idx.{Base64Url(createdAtIso)}.{Base64Url(id)}", with createdAt as the fixed-width
    // ISO-8601 "o" UTC form. Both parts are Base64Url so they are KV-key-safe single dot-free segments; the (createdAt, id)
    // order is recovered by decoding them (KV listing is unordered, so ordering is done client-side).
    private static string IndexKey(DateTimeOffset createdAt, string id)
        => string.Concat(IndexPrefix, Enc(createdAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)), ".", Enc(id));

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