// <copyright file="NatsJetStreamWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Globalization;
using Corvus.Text.Json.Internal;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>.
/// Each run's value is an envelope (the projected index header plus the opaque checkpoint); the KV entry's
/// native revision is the optimistic-concurrency token, and the single-owner lease lives in a second bucket
/// guarded by the same compare-and-set on revision.
/// </summary>
/// <remarks>
/// Wait/visibility queries scan the bucket's keys and filter on the index header. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class NatsJetStreamWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string RunsBucket = "arazzo_runs";
    private const string LeasesBucket = "arazzo_leases";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore runs;
    private readonly INatsKVStore leases;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamWorkflowStateStore(NatsConnection? ownedConnection, INatsKVStore runs, INatsKVStore leases, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.runs = runs;
        this.leases = leases;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Provisions the store's key/value buckets. Creating a KV bucket creates a JetStream stream, which
    /// requires stream-management permissions, so run this once at deploy/migration time, separately from the
    /// least-privileged account used to <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the store for operation (which needs only
    /// get/put/delete on the buckets' subjects).
    /// </summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the buckets exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(RunsBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(LeasesBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned key/value buckets.</summary>
    /// <remarks>
    /// This creates no streams/buckets, so it is safe to use a least-privileged account granted only
    /// get/put/delete on the buckets' subjects. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with a
    /// stream-management account — to create the buckets.
    /// </remarks>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowStateStore> ConnectAsync(
        string url,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore runs = await kv.GetStoreAsync(RunsBucket, cancellationToken).ConfigureAwait(false);
            INatsKVStore leases = await kv.GetStoreAsync(LeasesBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamWorkflowStateStore(connection, runs, leases, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Provisions the store's key/value buckets over a caller-supplied connection.</summary>
    /// <remarks>
    /// Supply a connection the caller configured (for example with a creds file, nkey, or token) so
    /// provisioning runs under a deliberate, stream-management-capable account. The caller retains ownership.
    /// </remarks>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the buckets exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(RunsBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(LeasesBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store over a caller-supplied connection (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example with a least-privileged operational account
    /// (get/put/delete on the buckets' subjects) — so the store runs under a least-privileged principal. This
    /// creates no buckets; call <see cref="PrepareAsync(INatsConnection, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowStateStore> ConnectAsync(
        INatsConnection connection,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore runs = await kv.GetStoreAsync(RunsBucket, cancellationToken).ConfigureAwait(false);
        INatsKVStore leases = await kv.GetStoreAsync(LeasesBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamWorkflowStateStore(ownedConnection: null, runs, leases, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, Envelope.Encode(index, checkpointUtf8.Span), expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] value, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        try
        {
            ulong revision = expected.IsNone
                ? await this.runs.CreateAsync(id.Value, value, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await this.runs.UpdateAsync(id.Value, value, ulong.Parse(expected.Value!, CultureInfo.InvariantCulture), cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WorkflowEtag(revision.ToString(CultureInfo.InvariantCulture));
        }
        catch (NatsKVException)
        {
            throw new WorkflowConflictException(id, expected);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.runs, id.Value, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } value })
        {
            return null;
        }

        byte[] checkpoint = Envelope.DecodeCheckpoint(value);
        return new WorkflowCheckpoint(checkpoint, new WorkflowEtag(entry.Value.Revision.ToString(CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");
        byte[] value = LeaseCodec.Encode(owner, token, expiresAt.ToUnixTimeMilliseconds());

        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.leases, id.Value, cancellationToken).ConfigureAwait(false);
        try
        {
            if (entry is not { Value: { } current })
            {
                await this.leases.CreateAsync(id.Value, value, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new WorkflowLease(id, owner, token, expiresAt);
            }

            (string currentOwner, _, long currentExpiresAt) = LeaseCodec.Decode(current);
            if (currentExpiresAt > now.ToUnixTimeMilliseconds() && currentOwner != owner)
            {
                return null;
            }

            await this.leases.UpdateAsync(id.Value, value, entry.Value.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(id, owner, token, expiresAt);
        }
        catch (NatsKVException)
        {
            // Another worker created or advanced the lease concurrently.
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.leases, lease.RunId.Value, cancellationToken).ConfigureAwait(false);
        if (entry is { Value: { } current } && LeaseCodec.Decode(current).Token == lease.Token)
        {
            await this.leases.DeleteAsync(lease.RunId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.PurgeAsync(this.runs, id.Value, cancellationToken).ConfigureAwait(false);
        await this.PurgeAsync(this.leases, id.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long cutoff = before.ToUnixTimeMilliseconds();
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            if (index.Status == WorkflowRunStatus.Suspended && index.DueAt is { } due && due.ToUnixTimeMilliseconds() <= cutoff)
            {
                yield return runId;
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            if (index.Status == WorkflowRunStatus.Suspended
                && index.AwaitingChannel == channel
                && (correlationId is null || index.AwaitingCorrelationId is null || index.AwaitingCorrelationId == correlationId))
            {
                yield return runId;
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, null, now, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        if (hostedWorkflowIds.Count == 0)
        {
            yield break;
        }

        var hosted = new HashSet<string>(hostedWorkflowIds);
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry entry) in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!hosted.Contains(entry.WorkflowId))
            {
                continue;
            }

            // §5.5 environment-scoped dispatch: a run pinned to an environment is claimable only by a runner
            // serving it; an unpinned run or an unscoped dispatcher matches anything.
            if (!MatchesEnvironment(entry.Environment, runnerEnvironment))
            {
                continue;
            }

            if (entry.Status == WorkflowRunStatus.Pending)
            {
                yield return runId;
                continue;
            }

            if (entry.Status == WorkflowRunStatus.Running)
            {
                NatsKVEntry<byte[]>? leaseEntry = await this.TryGetAsync(this.leases, runId.Value, cancellationToken).ConfigureAwait(false);
                bool live = false;
                if (leaseEntry is { Value: { } value })
                {
                    (_, _, long expiresAt) = LeaseCodec.Decode(value);
                    live = expiresAt > now.ToUnixTimeMilliseconds();
                }

                if (!live)
                {
                    yield return runId;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        // The KV bucket has no server-side ordering, so collect matches, sort by run id, and keyset-page here.
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        var listings = new List<WorkflowRunListing>();
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            if ((query.Status is not { } status || index.Status == status)
                && (query.WorkflowId is not { } workflowId || index.WorkflowId == workflowId)
                && (query.CreatedAfter is not { } createdAfter || index.CreatedAt >= createdAfter)
                && (query.CreatedBefore is not { } createdBefore || index.CreatedAt < createdBefore)
                && (query.UpdatedAfter is not { } updatedAfter || index.UpdatedAt >= updatedAfter)
                && (query.UpdatedBefore is not { } updatedBefore || index.UpdatedAt < updatedBefore)
                && (query.CorrelationId is not { } cid || index.CorrelationId == cid)
                && query.Tags.AllContainedIn(index.Tags)
                && (query.Security?.IsSatisfiedBy(index.SecurityTags) ?? true)
                && (after is null || string.CompareOrdinal(runId.Value, after) > 0))
            {
                listings.Add(new WorkflowRunListing(runId, index));
            }
        }

        listings.Sort(static (a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
        if (listings.Count > query.Limit + 1)
        {
            listings = listings.GetRange(0, query.Limit + 1);
        }

        return WorkflowContinuationToken.Paginate(listings, query.Limit);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<(WorkflowRunId Id, WorkflowRunIndexEntry Index)> ScanAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string key in this.runs.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.runs, key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value })
            {
                yield return (new WorkflowRunId(key), Envelope.DecodeIndex(value));
            }
        }
    }

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(INatsKVStore store, string key, CancellationToken cancellationToken)
    {
        try
        {
            return await store.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async ValueTask PurgeAsync(INatsKVStore store, string key, CancellationToken cancellationToken)
    {
        try
        {
            await store.PurgeAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

    // §5.5: an unscoped dispatcher (null runnerEnvironment) or an unpinned run (null entry environment) matches
    // anything; otherwise the run's pinned environment must equal the runner's.
    private static bool MatchesEnvironment(string? runEnvironment, string? runnerEnvironment)
        => runnerEnvironment is null || runEnvironment is null || string.Equals(runEnvironment, runnerEnvironment, StringComparison.Ordinal);

    private static class Envelope
    {
        private const int HeaderBufferSize = 512;

        public static byte[] Encode(in WorkflowRunIndexEntry index, ReadOnlySpan<byte> checkpoint)
        {
            // Serialize the index header through the pooled writer cache (not a fresh ArrayBufferWriter) — this is the
            // run-state write hotpath. The owned `result` (length-prefixed header + checkpoint) is the form the KV
            // driver demands; the header scratch is pooled.
            using JsonWorkspace workspace = JsonWorkspace.Create();
            Utf8JsonWriter writer = workspace.RentWriterAndBuffer(HeaderBufferSize, out IByteBufferWriter headerBuffer);
            try
            {
                writer.WriteStartObject();
                writer.WriteString("status", index.Status.ToString());
                writer.WriteString("workflowId", index.WorkflowId);
                writer.WriteNumber("createdAt", index.CreatedAt.ToUnixTimeMilliseconds());
                writer.WriteNumber("updatedAt", index.UpdatedAt.ToUnixTimeMilliseconds());
                if (index.DueAt is { } due)
                {
                    writer.WriteNumber("dueAt", due.ToUnixTimeMilliseconds());
                }

                if (index.AwaitingChannel is { } channel)
                {
                    writer.WriteString("awaitingChannel", channel);
                }

                if (index.AwaitingCorrelationId is { } correlationId)
                {
                    writer.WriteString("awaitingCorrelationId", correlationId);
                }

                if (index.ErrorType is { } errorType)
                {
                    writer.WriteString("errorType", errorType);
                }

                if (index.CorrelationId is { } runCorrelationId)
                {
                    writer.WriteString("correlationId", runCorrelationId);
                }

                if (index.Environment is { } environment)
                {
                    writer.WriteString("environment", environment);
                }

                if (!index.Tags.IsEmpty)
                {
                    writer.WritePropertyName("tags");
                    index.Tags.WriteTo(writer);
                }

                if (!index.SecurityTags.IsEmpty)
                {
                    writer.WritePropertyName("securityTags"u8);
                    index.SecurityTags.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.Flush();

                ReadOnlySpan<byte> header = headerBuffer.WrittenSpan;
                var result = new byte[4 + header.Length + checkpoint.Length];
                BinaryPrimitives.WriteInt32LittleEndian(result, header.Length);
                header.CopyTo(result.AsSpan(4));
                checkpoint.CopyTo(result.AsSpan(4 + header.Length));
                return result;
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(writer, headerBuffer);
            }
        }

        public static byte[] DecodeCheckpoint(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            return value.AsSpan(4 + headerLength).ToArray();
        }

        public static WorkflowRunIndexEntry DecodeIndex(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(value.AsMemory(4, headerLength));
            JsonElement root = document.RootElement;
            return new WorkflowRunIndexEntry(
                root.GetProperty("workflowId"u8).GetString()!,
                Enum.Parse<WorkflowRunStatus>(root.GetProperty("status"u8).GetString()!),
                DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("createdAt"u8).GetInt64()),
                DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("updatedAt"u8).GetInt64()),
                root.TryGetProperty("dueAt"u8, out JsonElement dueAt) ? DateTimeOffset.FromUnixTimeMilliseconds(dueAt.GetInt64()) : null,
                root.TryGetProperty("awaitingChannel"u8, out JsonElement channel) ? channel.GetString() : null,
                root.TryGetProperty("awaitingCorrelationId"u8, out JsonElement correlationId) ? correlationId.GetString() : null,
                root.TryGetProperty("errorType"u8, out JsonElement errorType) ? errorType.GetString() : null,
                root.TryGetProperty("correlationId"u8, out JsonElement queryCorrelationId) ? queryCorrelationId.GetString() : null,
                DecodeTags(root),
                DecodeSecurityTags(root),
                root.TryGetProperty("environment"u8, out JsonElement environment) ? environment.GetString() : null);
        }

        private static TagSet DecodeTags(JsonElement root)
            => root.TryGetProperty("tags"u8, out JsonElement tags) ? TagSet.CopyFrom(tags) : default;

        private static SecurityTagSet DecodeSecurityTags(JsonElement root)
            => root.TryGetProperty("securityTags"u8, out JsonElement securityTags) ? SecurityTagSet.CopyFrom(securityTags) : default;
    }

    private static class LeaseCodec
    {
        public static byte[] Encode(string owner, string token, long expiresAt)
            => PersistedJson.ToArray(
                (owner, token, expiresAt),
                static (Utf8JsonWriter writer, in (string Owner, string Token, long ExpiresAt) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("owner", c.Owner);
                    writer.WriteString("token", c.Token);
                    writer.WriteNumber("expiresAt", c.ExpiresAt);
                    writer.WriteEndObject();
                });

        public static (string Owner, string Token, long ExpiresAt) Decode(byte[] value)
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(value);
            JsonElement root = document.RootElement;
            return (
                root.GetProperty("owner"u8).GetString()!,
                root.GetProperty("token"u8).GetString()!,
                root.GetProperty("expiresAt"u8).GetInt64());
        }
    }
}