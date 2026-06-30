// <copyright file="RedisSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2). Each record is stored verbatim as its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) under a per-record key, with a set
/// per record kind for enumeration and a single counter key holding the monotonic generation a resolver caches
/// against. The etag travels inside the document, so optimistic concurrency is a read-compare-write.
/// </summary>
public sealed class RedisSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private const string RulePrefix = "arazzo:secrule:";
    private const string RuleIndexKey = "arazzo:secrules";
    private const string BindingPrefix = "arazzo:secbinding:";
    private const string BindingIndexKey = "arazzo:secbindings";
    private const string GenerationKey = "arazzo:secgen";

    // A keyset index for native binding paging by (order, id): a zero-scored sorted set whose members are
    // "{orderKey}\0{id}", where orderKey is a sign-preserving fixed-width hex of the int order so a lexicographic
    // (ZRANGEBYLEX) compare of members is (order asc, id asc) — the same total order the in-memory pager uses. Rules need
    // no such index: their keyset is the name, which is already the member of RuleIndexKey. Maintained on add/update/delete
    // (a binding update CAN change the order, so the old member is removed and the new one added).
    private const string BindingOrderIndexKey = "arazzo:secbindings:byorder";

    private const char MemberSeparator = '\0';

    // Singleton comparers (created once) for the client-side snapshot ordering, since the index sets are unordered: rules
    // by their name and bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) =>
        {
            // Compare the name's unescaped UTF-8 bytes (zero-alloc view) rather than realizing two managed strings; this is
            // also the COLLATE "C"/binary ordering the SQL backends key on.
            using UnescapedUtf8JsonString aName = a.RootElement.Name.GetUtf8String();
            using UnescapedUtf8JsonString bName = b.RootElement.Name.GetUtf8String();
            return aName.Span.SequenceCompareTo(bName.Span);
        });

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) =>
        {
            if (a.RootElement.OrderValue != b.RootElement.OrderValue)
            {
                return a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue);
            }

            // Compare the id's unescaped UTF-8 bytes (zero-alloc view) rather than realizing two managed strings; this is
            // also the COLLATE "C"/binary ordering the SQL backends key on.
            using UnescapedUtf8JsonString aId = a.RootElement.Id.GetUtf8String();
            using UnescapedUtf8JsonString bId = b.RootElement.Id.GetUtf8String();
            return aId.Span.SequenceCompareTo(bId.Span);
        });

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisSecurityPolicyStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens a security-policy store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisSecurityPolicyStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisSecurityPolicyStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a security-policy store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisSecurityPolicyStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisSecurityPolicyStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the RedisValue (a
        // ReadOnlyMemory<byte> carries the precise length, so there is no GC document array and no second copy). The
        // document is returned on success, disposed on failure.
        ParsedJsonDocument<SecurityRuleDocument> doc = SecurityPolicySerialization.SerializeNewRuleDoc(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            if (!await this.database.StringSetAsync(RulePrefix + name, utf8, when: When.NotExists).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"A security rule named '{name}' already exists.");
            }

            await this.database.SetAddAsync(RuleIndexKey, name).ConfigureAwait(false);
            await this.BumpGenerationAsync().ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RulePrefix + name).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<SecurityRuleDocument>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.ReadRulesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;
        string? after = DecodeRuleCursor(pageToken);
        string? qText = q.IsNotUndefined() ? (string)q : null;

        // Names are the set members (the keyset); sort ordinal client-side, skip past the cursor, then fetch only the page's
        // documents and apply q (name or expression) client-side — Redis has no server-side substring.
        RedisValue[] members = await this.database.SetMembersAsync(RuleIndexKey).ConfigureAwait(false);
        var names = new List<string>(members.Length);
        foreach (RedisValue m in members)
        {
            names.Add((string)m!);
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

                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RulePrefix + name).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue; // indexed but doc gone — skip
                }

                ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(lease.Span);
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

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING over the lease (the
        // lease stays alive through the synchronous etag check + merge).
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RulePrefix + name).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(lease.Memory);

        // Serialize the merged result into the pooled buffer the returned document owns and bind its exact bytes (no GC
        // array, no second copy); return on success, dispose on a write failure.
        ParsedJsonDocument<SecurityRuleDocument> updated = SecurityPolicySerialization.SerializeUpdatedRuleDoc(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await this.database.StringSetAsync(RulePrefix + name, utf8).ConfigureAwait(false);
            await this.BumpGenerationAsync().ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(RulePrefix, RuleIndexKey, "rule", name, expectedEtag, SecurityPolicySerialization.RuleEtagOf);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the RedisValue (no GC
        // document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<SecurityBindingDocument> doc = SecurityPolicySerialization.SerializeNewBindingDoc(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await this.database.StringSetAsync(BindingPrefix + id, utf8).ConfigureAwait(false);
            await this.database.SetAddAsync(BindingIndexKey, id).ConfigureAwait(false);
            await this.database.SortedSetAddAsync(BindingOrderIndexKey, BindingMember(draft.OrderValue, id), 0).ConfigureAwait(false);
            await this.BumpGenerationAsync().ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(BindingPrefix + id).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<SecurityBindingDocument>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.ReadBindingsAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);
        string? qText = q.IsNotUndefined() ? (string)q : null;

        // ZRANGEBYLEX returns the order-index members in (order, id) order; parse the id, fetch only the page's documents,
        // and apply q (claimType/claimValue/description) client-side.
        string? cursorMember = hasCursor ? BindingMember(cursorOrder, cursorId!) : null;
        RedisValue[] entries = await this.database.SortedSetRangeByValueAsync(BindingOrderIndexKey).ConfigureAwait(false);

        var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
        try
        {
            bool hasMore = false;
            foreach (RedisValue value in entries)
            {
                string member = (string)value!;
                if (cursorMember is not null && string.CompareOrdinal(member, cursorMember) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                int sep = member.IndexOf(MemberSeparator);
                string id = sep < 0 ? member : member[(sep + 1)..];
                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(BindingPrefix + id).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue; // indexed but doc gone — skip
                }

                ParsedJsonDocument<SecurityBindingDocument> document = PersistedJson.ToPooledDocument<SecurityBindingDocument>(lease.Span);
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

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING over the lease (the
        // lease stays alive through the synchronous etag check + merge).
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(BindingPrefix + id).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(lease.Memory);

        // Serialize the merged result into the pooled buffer the returned document owns and bind its exact bytes (no GC
        // array, no second copy); return on success, dispose on a write failure.
        ParsedJsonDocument<SecurityBindingDocument> updated = SecurityPolicySerialization.SerializeUpdatedBindingDoc(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await this.database.StringSetAsync(BindingPrefix + id, utf8).ConfigureAwait(false);

            // The order keyset can change on update, so refresh the order index: drop the old member and add the new one.
            int oldOrder = current.RootElement.OrderValue;
            int newOrder = draft.OrderValue;
            if (oldOrder != newOrder)
            {
                await this.database.SortedSetRemoveAsync(BindingOrderIndexKey, BindingMember(oldOrder, id)).ConfigureAwait(false);
                await this.database.SortedSetAddAsync(BindingOrderIndexKey, BindingMember(newOrder, id), 0).ConfigureAwait(false);
            }

            await this.BumpGenerationAsync().ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        RedisValue value = await this.database.StringGetAsync(BindingPrefix + id).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return false;
        }

        var raw = (byte[])value!;
        SecurityPolicySerialization.EnsureEtag("binding", id, expectedEtag, SecurityPolicySerialization.BindingEtagOf(raw));

        // Remove the order-index member too (its order comes from the stored document).
        using (ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(raw.AsMemory()))
        {
            await this.database.SortedSetRemoveAsync(BindingOrderIndexKey, BindingMember(current.RootElement.OrderValue, id)).ConfigureAwait(false);
        }

        await this.database.KeyDeleteAsync(BindingPrefix + id).ConfigureAwait(false);
        await this.database.SetRemoveAsync(BindingIndexKey, id).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PooledDocumentList<SecurityRuleDocument> rules = await this.ReadRulesAsync().ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindings = await this.ReadBindingsAsync().ConfigureAwait(false);
        RedisValue gen = await this.database.StringGetAsync(GenerationKey).ConfigureAwait(false);
        return new SecurityPolicySnapshot(rules, bindings, gen.IsNullOrEmpty ? 0 : (long)gen);
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

    // The order-index member for a binding: "{orderKey}\0{id}". orderKey is the int order with its sign bit flipped,
    // formatted as fixed-width hex, so a lexicographic compare of members is (order asc, id asc) — even across negative
    // orders — matching the in-memory pager and the ByBindingOrder comparer.
    private static string BindingMember(int order, string id)
        => string.Concat(((uint)(order ^ int.MinValue)).ToString("x8", CultureInfo.InvariantCulture), MemberSeparator.ToString(), id);

    // q matchers — Redis has no server-side substring, so q is applied client-side over the parsed page document; the
    // compared fields realise to managed strings only for this comparison (the documents themselves stay pooled/bytes-native).
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

    // Decodes the keyset cursor (order, id) from the request page token; the id reified to a string for the member compare.
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

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync()
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        foreach (RedisValue member in await this.database.SetMembersAsync(RuleIndexKey).ConfigureAwait(false))
        {
            // Read each candidate into a pooled lease (no GC read array); a list document must own its buffer, so copy the
            // lease span into an owned pooled document — the lease returns to the pool here.
            using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RulePrefix + (string)member!).ConfigureAwait(false);
            if (lease is { Length: > 0 })
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(lease.Span));
            }
        }

        list.Sort(ByRuleName);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync()
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        foreach (RedisValue member in await this.database.SetMembersAsync(BindingIndexKey).ConfigureAwait(false))
        {
            // Read each candidate into a pooled lease (no GC read array); a list document must own its buffer, so copy the
            // lease span into an owned pooled document — the lease returns to the pool here.
            using Lease<byte>? lease = await this.database.StringGetLeaseAsync(BindingPrefix + (string)member!).ConfigureAwait(false);
            if (lease is { Length: > 0 })
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(lease.Span));
            }
        }

        list.Sort(ByBindingOrder);
        return list;
    }

    private async ValueTask BumpGenerationAsync()
        => await this.database.StringIncrementAsync(GenerationKey).ConfigureAwait(false);

    private async ValueTask<bool> DeleteAsync(string prefix, string indexKey, string kind, string key, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf)
    {
        ArgumentNullException.ThrowIfNull(key);
        RedisValue value = await this.database.StringGetAsync(prefix + key).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, etagOf((byte[])value!));
        await this.database.KeyDeleteAsync(prefix + key).ConfigureAwait(false);
        await this.database.SetRemoveAsync(indexKey, key).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return true;
    }
}