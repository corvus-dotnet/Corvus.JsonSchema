// <copyright file="NatsJetStreamSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules +
/// claim→rule bindings, design §14.2) persisted in a single KV bucket. Each record is stored as its
/// Corvus.Text.Json schema document (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>)
/// under a namespaced, Base64Url-encoded key; a reserved <c>generation</c> key holds the monotonic generation a
/// resolver caches against. The etag travels inside the document, so optimistic concurrency is a read-compare-write.
/// </summary>
public sealed class NatsJetStreamSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_security";
    private const string RulePrefix = "rule.";
    private const string BindingPrefix = "binding.";
    private const string GenerationKey = "generation";

    // A keyset index for native binding paging by (order, id): one marker key per binding of the form
    // "bidx.{orderKey}.{Base64Url(id)}", where orderKey is a sign-preserving fixed-width hex of the int order. KV listing is
    // unordered, so the (order, id) order is materialised client-side by decoding orderKey + id from each index key — cheap
    // (no documents). Rules need no index: their keyset is the name, recoverable from the rule key. The index is maintained
    // on add/update (a binding update can change the order → old marker removed, new added) and delete.
    private const string BindingOrderIndexPrefix = "bidx.";

    private static readonly byte[] IndexMarker = "1"u8.ToArray();

    // Singleton comparers (created once) for the client-side snapshot ordering, since the KV key listing is unordered:
    // rules by their name and bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamSecurityPolicyStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the policy KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the policy KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamSecurityPolicyStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamSecurityPolicyStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamSecurityPolicyStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamSecurityPolicyStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        if (await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RulePrefix + Enc(name), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? ParsedJsonDocument<SecurityRuleDocument>.Parse(bytes.AsMemory()) : null;
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => this.ReadRulesAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;
        string? after = DecodeRuleCursor(pageToken);
        string? qText = q.IsNotUndefined() ? (string)q : null;

        // Rule keys encode the name (rule.{Base64Url(name)}); recover the name to materialise the keyset order client-side
        // (KV listing is unordered), then fetch only the page's documents and apply q (name/expression) client-side.
        var names = new List<string>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (key.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                names.Add(Dec(key[RulePrefix.Length..]));
            }
        }

        names.Sort(StringComparer.Ordinal);

        var page = new PooledDocumentList<SecurityRuleDocument>(pageSize);
        try
        {
            bool hasMore = false;
            foreach (string name in names)
            {
                if (after is not null && string.CompareOrdinal(name, after) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<SecurityRuleDocument> document = ParsedJsonDocument<SecurityRuleDocument>.Parse(bytes.AsMemory());
                if (qText is not null && !RuleMatches(document.RootElement, qText))
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
                return SecurityRulePage.Create(page);
            }

            using UnescapedUtf8JsonString lastName = page[page.Count - 1].Name.GetUtf8String();
            return SecurityRulePage.Create(page, lastName.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(bytes.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RulePrefix + Enc(name), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(RulePrefix, "rule", name, expectedEtag, SecurityPolicySerialization.RuleEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(BindingPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.store.PutAsync(BindingIndexKey(draft.OrderValue, id), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory()) : null;
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => this.ReadBindingsAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);
        string? qText = q.IsNotUndefined() ? (string)q : null;
        string? cursorOrderKey = hasCursor ? OrderKey(cursorOrder) : null;

        // Enumerate the order-index marker keys (cheap, no docs); recover (orderKey, id) from each and sort client-side by
        // (order, id) — orderKey lex == order numeric, the id decoded for the ordinal tiebreak. Then fetch only the page's
        // documents and apply q (claimType/claimValue/description) client-side.
        var entries = new List<(string OrderKey, string Id)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(BindingOrderIndexPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string rest = key[BindingOrderIndexPrefix.Length..];
            int dot = rest.IndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            entries.Add((rest[..dot], Dec(rest[(dot + 1)..])));
        }

        entries.Sort(static (a, b) =>
        {
            int byOrder = string.CompareOrdinal(a.OrderKey, b.OrderKey);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });

        var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
        try
        {
            bool hasMore = false;
            foreach ((string orderKey, string id) in entries)
            {
                if (hasCursor)
                {
                    int byOrder = string.CompareOrdinal(orderKey, cursorOrderKey);
                    bool after = byOrder > 0 || (byOrder == 0 && string.CompareOrdinal(id, cursorId) > 0);
                    if (!after)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }
                }

                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<SecurityBindingDocument> document = ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory());
                if (qText is not null && !BindingMatches(document.RootElement, qText))
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
                return SecurityBindingPage.Create(page);
            }

            SecurityBindingDocument last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return SecurityBindingPage.Create(page, last.OrderValue, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(BindingPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);

        // The order keyset can change on update, so refresh the order index: drop the old marker key and add the new one.
        int oldOrder = current.RootElement.OrderValue;
        int newOrder = draft.OrderValue;
        if (oldOrder != newOrder)
        {
            await this.DeleteKeyAsync(BindingIndexKey(oldOrder, id), cancellationToken).ConfigureAwait(false);
            await this.store.PutAsync(BindingIndexKey(newOrder, id), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag("binding", id, expectedEtag, SecurityPolicySerialization.BindingEtagOf(bytes));

        // Remove the order-index marker too (its order comes from the stored document).
        using (ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory()))
        {
            await this.DeleteKeyAsync(BindingIndexKey(current.RootElement.OrderValue, id), cancellationToken).ConfigureAwait(false);
        }

        await this.store.DeleteAsync(BindingPrefix + Enc(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        PooledDocumentList<SecurityRuleDocument> rules = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindings = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        NatsKVEntry<byte[]>? gen = await this.TryGetAsync(GenerationKey, cancellationToken).ConfigureAwait(false);
        long generation = gen is { Value: { } bytes } && long.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out long g) ? g : 0;
        return new SecurityPolicySnapshot(rules, bindings, generation);
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

    // The order-index marker key for a binding: "bidx.{orderKey}.{Base64Url(id)}". orderKey is the int order with its sign
    // bit flipped, fixed-width hex (KV-key-safe, no '.'), so decoding (orderKey, id) and sorting client-side yields
    // (order asc, id asc) even across negative orders. KV listing is unordered, so the order is materialised client-side.
    private static string BindingIndexKey(int order, string id)
        => string.Concat(BindingOrderIndexPrefix, OrderKey(order), ".", Enc(id));

    private static string OrderKey(int order)
        => ((uint)(order ^ int.MinValue)).ToString("x8", CultureInfo.InvariantCulture);

    // q matchers — KV has no server-side substring, so q is applied client-side over the parsed page document; the compared
    // fields realise to managed strings only for this comparison (the documents themselves stay pooled/bytes-native).
    private static bool RuleMatches(in SecurityRuleDocument rule, string q)
        => rule.NameValue.Contains(q, StringComparison.OrdinalIgnoreCase)
        || rule.ExpressionValue.Contains(q, StringComparison.OrdinalIgnoreCase);

    private static bool BindingMatches(in SecurityBindingDocument binding, string q)
    {
        if (binding.ClaimTypeValue.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (binding.ClaimValue.IsNotUndefined() && ((string)binding.ClaimValue).Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return binding.Description.IsNotUndefined() && ((string)binding.Description).Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    // Decodes the keyset cursor (rule name) from the request page token; reified to a string for the client-side compare.
    private static string? DecodeRuleCursor(JsonString pageToken)
    {
        if (!pageToken.IsNotUndefined())
        {
            return null;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SecurityRuleContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
        try
        {
            return SecurityRuleContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> nameUtf8)
                ? Encoding.UTF8.GetString(nameUtf8)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Decodes the keyset cursor (order, id) from the request page token; the id reified to a string for the compare.
    private static bool DecodeBindingCursor(JsonString pageToken, out int order, out string? id)
    {
        order = 0;
        id = null;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SecurityBindingContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
        try
        {
            if (SecurityBindingContinuationToken.TryDecode(tokenUtf8.Span, buffer, out order, out ReadOnlySpan<byte> idUtf8))
            {
                id = Encoding.UTF8.GetString(idUtf8);
                return true;
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask DeleteKeyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(bytes.AsMemory()));
            }
        }

        list.Sort(ByRuleName);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(BindingPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory()));
            }
        }

        list.Sort(ByBindingOrder);
        return list;
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? gen = await this.TryGetAsync(GenerationKey, cancellationToken).ConfigureAwait(false);
        long current = gen is { Value: { } bytes } && long.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out long g) ? g : 0;
        await this.store.PutAsync(GenerationKey, Encoding.UTF8.GetBytes((current + 1).ToString(CultureInfo.InvariantCulture)), cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<bool> DeleteAsync(string prefix, string kind, string key, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(prefix + Enc(key), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, etagOf(bytes));
        await this.store.DeleteAsync(prefix + Enc(key), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}