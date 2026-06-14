// <copyright file="NatsJetStreamSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        if (await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewRule(name, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RulePrefix + Enc(name), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? PersistedJson.ToPooledDocument<SecurityRuleDocument>(bytes) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => new PooledDocumentList<SecurityRuleDocument>(await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(bytes, "rule", name, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RulePrefix + Enc(name), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(RulePrefix, "rule", name, expectedEtag, SecurityPolicySerialization.RuleEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(BindingPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? PersistedJson.ToPooledDocument<SecurityBindingDocument>(bytes) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => new PooledDocumentList<SecurityBindingDocument>(await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(bytes, "binding", id, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(BindingPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(BindingPrefix, "binding", id, expectedEtag, SecurityPolicySerialization.BindingEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var rules = new PooledDocumentList<SecurityRuleDocument>(await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false));
        var bindings = new PooledDocumentList<SecurityBindingDocument>(await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false));
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

    private async ValueTask<List<ParsedJsonDocument<SecurityRuleDocument>>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new List<ParsedJsonDocument<SecurityRuleDocument>>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(bytes));
            }
        }

        list.Sort(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));
        return list;
    }

    private async ValueTask<List<ParsedJsonDocument<SecurityBindingDocument>>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new List<ParsedJsonDocument<SecurityBindingDocument>>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(BindingPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(bytes));
            }
        }

        list.Sort(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));
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