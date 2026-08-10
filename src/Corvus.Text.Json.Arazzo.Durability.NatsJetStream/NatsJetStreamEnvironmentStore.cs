// <copyright file="NatsJetStreamEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted in a single
/// KV bucket. Each environment is stored as its <see cref="Environment"/> document under a namespaced key encoding (Name
/// and a discriminator over its immutable management tags), so reach-isolated environments that share a name coexist;
/// its etag travels inside the document, independent of the KV revision.
/// </summary>
/// <remarks>
/// <para>Each KV key is <c>env.{base64url(name)}.{base64url(discriminator)}</c>: Base64Url over the UTF-8 of each
/// component yields only the restricted set of characters a NATS KV key permits, and the <c>.</c> separators let the
/// candidates for a name be enumerated by key prefix, mirroring how the catalog and security-policy stores prefix-scan
/// the bucket.</para>
/// <para>Point management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. List/count narrow through
/// the store's §14.4 label bucket first — the reach resolves to candidate keys by subject-filtered key listings — so
/// only candidate documents are ever read, and the exact reach then decides each one; a re-tagging update re-points the
/// label entries around the document write in the §14.4 ordering.</para>
/// </remarks>
public sealed class NatsJetStreamEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_environments";
    private const string KeyPrefix = "env.";

    private static readonly byte[] EmptyLabelValue = [];

    // The deployment's single tenancy-ledger key (ADR 0065). Deliberately outside KeyPrefix: every environment scan
    // filters on that prefix, so the ledger shares the bucket without ever appearing as a candidate environment.
    private const string TenancyLedgerKey = "tenancy.ledger";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly INatsKVStore labels;
    private readonly NatsSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamEnvironmentStore(NatsConnection? ownedConnection, INatsKVStore store, INatsKVStore labels, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.labels = labels;
        this.labelIndex = new NatsSecurityLabelIndex(labels);
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the environments KV bucket (requires stream-management rights); run once at deploy time.</summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.EnvironmentLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the environments KV bucket over a caller-supplied connection.</summary>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.EnvironmentLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned KV bucket.</summary>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamEnvironmentStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.EnvironmentLabelBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamEnvironmentStore(connection, store, labels, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamEnvironmentStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.EnvironmentLabelBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamEnvironmentStore(ownedConnection: null, store, labels, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string key = Key(draft.NameValue, SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue));

        // §14.4 label entries: ADDED before the environment becomes visible, so an interrupted add can only leave
        // an entry pointing at a row that never landed — which the exact reach evaluation discards — never a
        // visible row with no entry, which would hide it from a narrowed list.
        foreach (string entryKey in NatsSecurityLabels.EntryKeysFor(draft.ManagementTagsValue, key))
        {
            await this.labels.PutAsync(entryKey, EmptyLabelValue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // Create is optimistic-create (fails if the key already holds a live value), giving the exact-duplicate
            // rejection the relational backends get from their primary-key unique violation.
            await this.store.CreateAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVException)
        {
            ThrowHelper.ThrowEnvironmentAlreadyExists(draft.NameValue);
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = EnvironmentContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // §14.4: narrow to the candidate keys the label bucket admits before reading anything. A null answer means
        // the index could not narrow and the keys-only scan runs as it always did; an empty one means no environment
        // qualifies, so the page is empty without a single doc read. The exact reach below still decides every
        // candidate (the plan is a sound over-approximation), so an imprecise plan costs throughput and can never
        // widen reach.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return EnvironmentPage.Create(PooledDocumentList<Environment>.Empty);
        }

        // KV listing is unordered and there is no server-side range query, so the stable total order — the contractual
        // name plus the discriminator as a tie-breaker — is materialised in process from the keys alone (with
        // candidates in hand, from those keys; otherwise a cheap keys-only scan). Each key is
        // env.{Enc(name)}.{Enc(discriminator)}, so decoding its two Base64Url parts recovers the ordering tuple
        // without reading a single document; a stale label entry resolving to a deleted key falls into the
        // absent-entry skip below.
        var ordered = new List<(string Name, string Discriminator, string Key)>();
        if (candidates is null)
        {
            await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out (string Name, string Discriminator) parts))
                {
                    continue;
                }

                ordered.Add((parts.Name, parts.Discriminator, key));
            }
        }
        else
        {
            foreach (string key in candidates)
            {
                if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out (string Name, string Discriminator) parts))
                {
                    continue;
                }

                ordered.Add((parts.Name, parts.Discriminator, key));
            }
        }

        ordered.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            string lastName = string.Empty, lastDisc = string.Empty;
            foreach ((string name, string discriminator, string key) in ordered)
            {
                // Seek strictly past the cursor in (name, discriminator) order.
                if (hasCursor && CompareCursor(name, discriminator, cursor) <= 0)
                {
                    continue;
                }

                // Fetch the document lazily — only for keys at/after the cursor, and only until the page fills plus one.
                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue;
                }

                ParsedJsonDocument<Environment> cand = PersistedJson.ToPooledDocument<Environment>(bytes);
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
                    lastName = name;
                    lastDisc = discriminator;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore ? EnvironmentPage.Create(docs, lastName, lastDisc) : EnvironmentPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? key) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The row is addressed by its frozen create-time key (ADR 0067). A draft that supplies management tags
        // re-tags the row's reach scope (a store-level replace; an omitted set is carried forward), so the §14.4
        // label entries are re-pointed by the diff around the document write, in the §14.4 ordering — entries for
        // the NEW tags are added before the doc carries them and the removed ones are dropped only after it stops,
        // so an interruption can only leave a stale entry (discarded by the exact evaluation), never a hidden row.
        HashSet<string>? previousKeys = null;
        HashSet<string>? desiredKeys = null;
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            SecurityTagSet previousTags;
            using (ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(existing))
            {
                previousTags = SecurityTagSet.CopyFrom(current.RootElement.ManagementTags);
            }

            previousKeys = NatsSecurityLabels.EntryKeysFor(previousTags, key!);
            desiredKeys = NatsSecurityLabels.EntryKeysFor(draft.ManagementTagsValue, key!);
            foreach (string entryKey in desiredKeys)
            {
                if (!previousKeys.Contains(entryKey))
                {
                    await this.labels.PutAsync(entryKey, EmptyLabelValue, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await this.store.PutAsync(key!, json, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (previousKeys is not null && desiredKeys is not null)
        {
            foreach (string entryKey in previousKeys)
            {
                if (!desiredKeys.Contains(entryKey))
                {
                    await this.PurgeLabelAsync(entryKey, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? key) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        // The label entries derive from the CURRENT doc tags (a re-tag re-pointed them in step), read before the row
        // goes, and dropped only after it — the §14.4 ordering: an interrupted delete leaves a stale entry, harmless
        // when nothing loads behind it, whereas dropping entries first would strand a still-visible row.
        HashSet<string> entryKeys;
        using (ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(existing))
        {
            entryKeys = NatsSecurityLabels.EntryKeysFor(SecurityTagSet.CopyFrom(current.RootElement.ManagementTags), key!);
        }

        await this.store.DeleteAsync(key!, cancellationToken: cancellationToken).ConfigureAwait(false);
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
    // string to the empty string — and a NATS subject cannot contain an empty token (it would leave a trailing dot in
    // the key). An environment with no management tags has an empty tag discriminator, so empty is mapped to the single
    // char "_" instead: Base64Url output of non-empty input is always ≥ 2 chars, so the length-1 sentinel never collides
    // with a real encoding, and Dec inverts it before decoding.
    private static string Enc(string value) => value.Length == 0 ? "_" : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string value) => value == "_" ? string.Empty : Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    // Inverts Key: splits env.{Enc(name)}.{Enc(discriminator)} on the dot separators and Base64Url-decodes each part
    // back to its original string, so the ordering tuple is recovered from the key alone.
    private static bool TryParseKey(string key, out (string Name, string Discriminator) parts)
    {
        parts = default;
        ReadOnlySpan<char> body = key.AsSpan(KeyPrefix.Length);
        int firstDot = body.IndexOf('.');
        if (firstDot < 0)
        {
            return false;
        }

        ReadOnlySpan<char> namePart = body[..firstDot];
        ReadOnlySpan<char> discPart = body[(firstDot + 1)..];
        if (discPart.IndexOf('.') >= 0)
        {
            return false;
        }

        try
        {
            parts = (Dec(namePart.ToString()), Dec(discPart.ToString()));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Orders a row's (name, discriminator) against the page cursor in the stable total order.
    private static int CompareCursor(string name, string discriminator, (string Name, string TieBreaker) cursor)
    {
        int byName = string.CompareOrdinal(name, cursor.Name);
        return byName != 0 ? byName : string.CompareOrdinal(discriminator, cursor.TieBreaker);
    }

    // The KV key for a single environment: the namespace, then Base64Url(name), Base64Url(discriminator), dot-separated.
    // Base64Url emits only [A-Za-z0-9_-], all valid KV key characters, so the components — which may contain dots,
    // control characters, etc. — round-trip safely and the dot separators delimit the prefix levels used for enumeration.
    private static string Key(string name, string discriminator)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(name)}.{Enc(discriminator)}");

    // The name prefix (without the trailing dot) shared by every environment's key for that name.
    private static string KeyPrefixFor(string name)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(name)}.");

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its KV
    // key (the write-back target). An environment outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Key)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string prefix = KeyPrefixFor(name);
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(bytes);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (bytes, key);
            }
        }

        return (null, null);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<TenancyLedger>?> GetTenancyLedgerAsync(CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? found = await this.TryGetAsync(TenancyLedgerKey, cancellationToken).ConfigureAwait(false);
        return found?.Value is { } json ? PersistedJson.ToPooledDocument<TenancyLedger>(json) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryCommitTenancyLedgerAsync(TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        byte[] json = TenancyLedgerSerialization.SerializeCommitted(current, admitting, actor, this.timeProvider.GetUtcNow(), NewEtag());
        if (current.IsUndefined())
        {
            try
            {
                // Optimistic create: it fails if the key already holds a live value, which is "no row must exist".
                await this.store.CreateAsync(TenancyLedgerKey, json, cancellationToken: cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (NatsKVException)
            {
                return false;
            }
        }

        // The bucket's revision is the swap token and the stored etag is what the caller decided against. Both windows
        // are covered: a writer landing before this read is caught by the etag check, and one landing between the read
        // and the update is caught by the revision CAS.
        NatsKVEntry<byte[]>? found = await this.TryGetAsync(TenancyLedgerKey, cancellationToken).ConfigureAwait(false);
        if (found is not { } entry || entry.Value is not { } stored || !TenancyLedgerSerialization.CarriesEtagOf(stored, current))
        {
            return false;
        }

        try
        {
            await this.store.UpdateAsync(TenancyLedgerKey, json, entry.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NatsKVException)
        {
            return false;
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