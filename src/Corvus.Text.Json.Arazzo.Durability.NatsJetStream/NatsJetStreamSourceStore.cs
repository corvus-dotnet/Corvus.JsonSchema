// <copyright file="NatsJetStreamSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted in a single
/// KV bucket. Each source is stored as its <see cref="RegisteredSource"/> document under a namespaced key encoding (Name
/// and a discriminator over its immutable management tags), so reach-isolated sources that share a name coexist;
/// its etag travels inside the document, independent of the KV revision.
/// </summary>
/// <remarks>
/// <para>Each KV key is <c>source.{base64url(name)}.{base64url(discriminator)}</c>: Base64Url over the UTF-8 of each
/// component yields only the restricted set of characters a NATS KV key permits, and the <c>.</c> separators let the
/// candidates for a name be enumerated by key prefix, mirroring how the catalog and security-policy stores prefix-scan
/// the bucket.</para>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in
/// memory over the small candidate set for a name, since a deployment keeps those reach-disjoint. The KV store has no
/// server-side ordering or filtering, so the filter and the list ordering are done in process — the only correct option
/// for a key/value backend.</para>
/// </remarks>
public sealed class NatsJetStreamSourceStore : ISourceStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_sources";
    private const string KeyPrefix = "source.";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamSourceStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the sources KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the sources KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamSourceStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamSourceStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamSourceStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamSourceStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string key = Key(draft.NameValue, SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue));
        try
        {
            // Create is optimistic-create (fails if the key already holds a live value), giving the exact-duplicate
            // rejection the relational backends get from their primary-key unique violation.
            await this.store.CreateAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVException)
        {
            throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
        }

        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourcePage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // KV listing is unordered and there is no server-side range query, so the stable total order — the contractual
        // name plus the discriminator as a tie-breaker — is materialised in process from the keys alone (a cheap
        // keys-only scan). Each key is source.{Enc(name)}.{Enc(discriminator)}, so decoding its two Base64Url parts
        // recovers the ordering tuple without reading a single document.
        var ordered = new List<(string Name, string Discriminator, string Key)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out (string Name, string Discriminator) parts))
            {
                continue;
            }

            ordered.Add((parts.Name, parts.Discriminator, key));
        }

        ordered.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
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

                ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(bytes);
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

            return hasMore ? SourcePage.Create(docs, lastName, lastDisc) : SourcePage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? key) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.store.PutAsync(key!, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
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
            SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
        }

        await this.store.DeleteAsync(key!, cancellationToken: cancellationToken).ConfigureAwait(false);
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
    // the key). A source with no management tags has an empty tag discriminator, so empty is mapped to the single
    // char "_" instead: Base64Url output of non-empty input is always ≥ 2 chars, so the length-1 sentinel never collides
    // with a real encoding, and Dec inverts it before decoding.
    private static string Enc(string value) => value.Length == 0 ? "_" : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string value) => value == "_" ? string.Empty : Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    // Inverts Key: splits source.{Enc(name)}.{Enc(discriminator)} on the dot separators and Base64Url-decodes each part
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

    // The KV key for a single source: the namespace, then Base64Url(name), Base64Url(discriminator), dot-separated.
    // Base64Url emits only [A-Za-z0-9_-], all valid KV key characters, so the components — which may contain dots,
    // control characters, etc. — round-trip safely and the dot separators delimit the prefix levels used for enumeration.
    private static string Key(string name, string discriminator)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(name)}.{Enc(discriminator)}");

    // The name prefix (without the trailing dot) shared by every source's key for that name.
    private static string KeyPrefixFor(string name)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(name)}.");

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its KV
    // key (the write-back target). A source outside reach is invisible (non-disclosing).
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

            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(bytes);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (bytes, key);
            }
        }

        return (null, null);
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