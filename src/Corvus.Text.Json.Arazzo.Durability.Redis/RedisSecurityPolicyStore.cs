// <copyright file="RedisSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
        byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
        if (!await this.database.StringSetAsync(RulePrefix + name, json, when: When.NotExists).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await this.database.SetAddAsync(RuleIndexKey, name).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.StringGetAsync(RulePrefix + name).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : PersistedJson.ToPooledDocument<SecurityRuleDocument>((byte[])value!);
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
        RedisValue value = await this.database.StringGetAsync(RulePrefix + name).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule((byte[])value!, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.database.StringSetAsync(RulePrefix + name, json).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(RulePrefix, RuleIndexKey, "rule", name, expectedEtag, SecurityPolicySerialization.RuleEtagOf);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.database.StringSetAsync(BindingPrefix + id, json).ConfigureAwait(false);
        await this.database.SetAddAsync(BindingIndexKey, id).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.StringGetAsync(BindingPrefix + id).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : PersistedJson.ToPooledDocument<SecurityBindingDocument>((byte[])value!);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.ReadBindingsAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        RedisValue value = await this.database.StringGetAsync(BindingPrefix + id).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding((byte[])value!, "binding", id, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.database.StringSetAsync(BindingPrefix + id, json).ConfigureAwait(false);
        await this.BumpGenerationAsync().ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
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
            RedisValue value = await this.database.StringGetAsync(RulePrefix + (string)member!).ConfigureAwait(false);
            if (!value.IsNullOrEmpty)
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>((byte[])value!));
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
            RedisValue value = await this.database.StringGetAsync(BindingPrefix + (string)member!).ConfigureAwait(false);
            if (!value.IsNullOrEmpty)
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>((byte[])value!));
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