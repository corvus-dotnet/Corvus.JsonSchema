// <copyright file="RedisSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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

    // Singleton comparers (created once) for the client-side snapshot ordering, since the index sets are unordered: rules
    // by their name and bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

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
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(BindingPrefix, BindingIndexKey, "binding", id, expectedEtag, SecurityPolicySerialization.BindingEtagOf);

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