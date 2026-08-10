// <copyright file="NatsJetStreamWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted in a single KV bucket so a working copy survives a restart. Each working copy is stored as its
/// <see cref="WorkspaceWorkflow"/> document under a key encoding its server-minted <c>id</c> (globally unique, so the id
/// alone is the key — no Name/Tags composite); its etag travels inside the document, independent of the KV revision.
/// </summary>
/// <remarks>
/// <para>Each KV key is <c>wc.{base64url(id)}</c>: Base64Url over the UTF-8 of the id yields only the restricted set of
/// characters a NATS KV key permits (any id string round-trips safely, so a client-supplied id can never form an invalid
/// key), and the <c>.</c> separator lets the working copies be enumerated by key prefix. Because the id is the sole
/// unique key, get/update/delete resolve it to a single key and do a point lookup — the KV equivalent of the relational
/// backends' <c>WHERE Id = @id</c> — rather than a scan.</para>
/// <para>Point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — a working copy
/// outside reach is reported as absent (non-disclosing). List/count narrow through the store's §14.4 label bucket
/// first — the reach resolves to candidate keys by subject-filtered key listings — so only candidate documents are
/// ever read, and the exact reach then decides each one; the tags are immutable on save, so the entries never need an
/// update-side re-point. The document is carried bytes-to-bytes (#803): rows read/write via
/// <see cref="ParsedJsonDocument{T}"/> and the shared pooled serialization over the raw KV bytes, never a per-op
/// detached clone.</para>
/// </remarks>
public sealed class NatsJetStreamWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_workspace_workflows";
    private const string KeyPrefix = "wc.";

    private static readonly byte[] EmptyLabelValue = [];

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly INatsKVStore labels;
    private readonly NatsSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamWorkspaceWorkflowStore(NatsConnection? ownedConnection, INatsKVStore store, INatsKVStore labels, TimeProvider timeProvider)
    {
        this.labels = labels;
        this.labelIndex = new NatsSecurityLabelIndex(labels);
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the working-copy KV bucket (requires stream-management rights); run once at deploy time.</summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.WorkspaceWorkflowLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the working-copy KV bucket over a caller-supplied connection.</summary>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.WorkspaceWorkflowLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned KV bucket.</summary>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamWorkspaceWorkflowStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.WorkspaceWorkflowLabelBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamWorkspaceWorkflowStore(connection, store, labels, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamWorkspaceWorkflowStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.WorkspaceWorkflowLabelBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamWorkspaceWorkflowStore(ownedConnection: null, store, labels, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential; the
        // id is opaque to clients either way). Base64Url of the id orders ordinally under the same total order the keyset
        // pager compares. A fresh GUID is globally unique, so CreateAsync (optimistic-create) never collides here — it
        // gives the same "insert a new row" shape the relational backends get from their INSERT.
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);

        // §14.4 label entries: ADDED before the working copy becomes visible, so an interrupted add can only leave
        // an entry pointing at a row that never landed — which the exact reach evaluation discards — never a
        // visible row with no entry, which would hide it from a narrowed list.
        string key = Key(id);
        foreach (string entryKey in NatsSecurityLabels.EntryKeysFor(draft.ManagementTagsValue, key))
        {
            await this.labels.PutAsync(entryKey, EmptyLabelValue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await this.store.CreateAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? json = await this.FindForManagementAsync(id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // §14.4: narrow to the candidate keys the label bucket admits before reading anything. A null answer means
        // the index could not narrow and the keys-only scan runs as it always did; an empty one means no working
        // copy qualifies, so the page is empty without a single doc read. The exact reach below still decides every
        // candidate (the plan is a sound over-approximation), so an imprecise plan costs throughput and can never
        // widen reach.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return WorkspaceWorkflowPage.Create(PooledDocumentList<WorkspaceWorkflow>.Empty);
        }

        // KV listing is unordered and there is no server-side range query, so the stable total order — the id (globally
        // unique, so the id alone is the whole order and the tie-breaker is empty) — is materialised in process from the
        // keys alone (with candidates in hand, from those keys; otherwise a cheap keys-only scan). Each key is
        // wc.{Enc(id)}, so decoding its Base64Url body recovers the ordering id without reading a single document; a
        // stale label entry resolving to a deleted key falls into the absent-entry skip below.
        var ordered = new List<(string Id, string Key)>();
        if (candidates is null)
        {
            await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out string id))
                {
                    continue;
                }

                ordered.Add((id, key));
            }
        }
        else
        {
            foreach (string key in candidates)
            {
                if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out string id))
                {
                    continue;
                }

                ordered.Add((id, key));
            }
        }

        ordered.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            string lastId = string.Empty;
            foreach ((string id, string key) in ordered)
            {
                // Seek strictly past the cursor in id order.
                if (hasCursor && string.CompareOrdinal(id, cursor.Id) <= 0)
                {
                    continue;
                }

                // Fetch the document lazily — only for keys at/after the cursor, and only until the page fills plus one.
                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue;
                }

                ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(bytes);
                bool kept = false;
                try
                {
                    SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tags))
                    {
                        continue;
                    }

                    if (docs.Count == pageSize)
                    {
                        // A further visible row exists beyond the page: emit a token pointing at the last included row.
                        hasMore = true;
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastId = id;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore
                ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                : WorkspaceWorkflowPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.store.PutAsync(Key(id), json, cancellationToken: cancellationToken).ConfigureAwait(false); // the id, provenance, and tags are immutable → key unchanged
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        // The label entries derive from the doc tags (immutable on save), read before the row goes, and dropped only
        // after it — the §14.4 ordering: an interrupted delete leaves a stale entry, harmless when nothing loads
        // behind it, whereas dropping entries first would strand a still-visible row.
        string key = Key(id);
        HashSet<string> entryKeys;
        using (ParsedJsonDocument<WorkspaceWorkflow> current = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(existing))
        {
            entryKeys = NatsSecurityLabels.EntryKeysFor(SecurityTagSet.CopyFrom(current.RootElement.ManagementTags), key);
        }

        await this.store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (string entryKey in entryKeys)
        {
            await this.PurgeLabelAsync(entryKey, cancellationToken).ConfigureAwait(false);
        }

        return true;
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

    // Base64Url over the UTF-8 bytes yields only [A-Za-z0-9_-] (all valid KV subject-token chars), but it maps the empty
    // string to the empty string — and a NATS subject cannot contain an empty token (it would leave a trailing dot in the
    // key). A minted id is never empty, but a client may pass any id string to get/update/delete, so empty is mapped to
    // the single char "_" instead: Base64Url output of non-empty input is always ≥ 2 chars, so the length-1 sentinel
    // never collides with a real encoding, and Dec inverts it before decoding.
    private static string Enc(string value) => value.Length == 0 ? "_" : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string value) => value == "_" ? string.Empty : Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    // The KV key for a single working copy: the namespace, then Base64Url(id). Base64Url emits only [A-Za-z0-9_-], all
    // valid KV key characters, so any id string — including one a client supplies — round-trips safely and forms a valid
    // key; the dot separator delimits the prefix level used for enumeration.
    private static string Key(string id)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(id)}");

    // Inverts Key: takes the single token after wc. and Base64Url-decodes it back to the id, so the ordering id is
    // recovered from the key alone. A key with a further dot is not one of ours and is rejected.
    private static bool TryParseKey(string key, out string id)
    {
        id = string.Empty;
        ReadOnlySpan<char> body = key.AsSpan(KeyPrefix.Length);
        if (body.IndexOf('.') >= 0)
        {
            return false;
        }

        try
        {
            id = Dec(body.ToString());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key, so a point lookup on its deterministic key suffices). A working copy outside reach is invisible
    // (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(bytes);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? bytes : null;
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

    // Resolves the reach to the candidate KV keys the label bucket admits (§14.4). Null means "the index could not
    // narrow this rule", which is a legitimate answer — the exact evaluation downstream still decides what the
    // principal sees, so an un-narrowable rule costs throughput and never widens reach.
    private ValueTask<IReadOnlySet<string>?> ResolveReachCandidatesAsync(SecurityFilter? security, CancellationToken cancellationToken)
        => security is null
            ? new ValueTask<IReadOnlySet<string>?>((IReadOnlySet<string>?)null)
            : SecurityLabelQueryResolver.ResolveAsync(
                security.ToPredicate(SecurityLabelQueryEmitter.Instance), this.labelIndex, cancellationToken);

    private async ValueTask PurgeLabelAsync(string entryKey, CancellationToken cancellationToken)
    {
        try
        {
            await this.labels.PurgeAsync(entryKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }
}