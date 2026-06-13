// <copyright file="NatsJetStreamSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
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
    public async ValueTask<SecurityRuleRecord> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        if (await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        var record = new SecurityRuleRecord(name, definition.Expression, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        await this.store.PutAsync(RulePrefix + Enc(name), SecurityRuleDocument.From(record).ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? SecurityRuleDocument.FromJson(bytes).ToRecord() : null;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityRuleRecord>> ListRulesAsync(CancellationToken cancellationToken)
        => await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RulePrefix + Enc(name), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        SecurityRuleRecord current = SecurityRuleDocument.FromJson(bytes).ToRecord();
        EnsureEtag("rule", name, expectedEtag, current.Etag);
        var updated = current with
        {
            Expression = definition.Expression,
            Description = definition.Description,
            UpdatedBy = actor,
            UpdatedAt = this.timeProvider.GetUtcNow(),
            Etag = NewEtag(),
        };
        await this.store.PutAsync(RulePrefix + Enc(name), SecurityRuleDocument.From(updated).ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(RulePrefix, "rule", name, expectedEtag, doc => SecurityRuleDocument.FromJson(doc).ToRecord().Etag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        var record = new SecurityBinding(id, definition.ClaimType, definition.ClaimValue, definition.Read, definition.Write, definition.Purge, definition.Order, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        await this.store.PutAsync(BindingPrefix + Enc(id), SecurityBindingDocument.From(record).ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? SecurityBindingDocument.FromJson(bytes).ToRecord() : null;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityBinding>> ListBindingsAsync(CancellationToken cancellationToken)
        => await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(BindingPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        SecurityBinding current = SecurityBindingDocument.FromJson(bytes).ToRecord();
        EnsureEtag("binding", id, expectedEtag, current.Etag);
        var updated = current with
        {
            ClaimType = definition.ClaimType,
            ClaimValue = definition.ClaimValue,
            Read = definition.Read,
            Write = definition.Write,
            Purge = definition.Purge,
            Order = definition.Order,
            Description = definition.Description,
            UpdatedBy = actor,
            UpdatedAt = this.timeProvider.GetUtcNow(),
            Etag = NewEtag(),
        };
        await this.store.PutAsync(BindingPrefix + Enc(id), SecurityBindingDocument.From(updated).ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(BindingPrefix, "binding", id, expectedEtag, doc => SecurityBindingDocument.FromJson(doc).ToRecord().Etag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SecurityRuleRecord> rules = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityBinding> bindings = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
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

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private async ValueTask<IReadOnlyList<SecurityRuleRecord>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new List<SecurityRuleRecord>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(SecurityRuleDocument.FromJson(bytes).ToRecord());
            }
        }

        list.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }

    private async ValueTask<IReadOnlyList<SecurityBinding>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new List<SecurityBinding>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(BindingPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                list.Add(SecurityBindingDocument.FromJson(bytes).ToRecord());
            }
        }

        list.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
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

        EnsureEtag(kind, key, expectedEtag, etagOf(bytes));
        await this.store.DeleteAsync(prefix + Enc(key), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}