// <copyright file="AzureStorageSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules +
/// claim→rule bindings, design §14.2). Each record is one entity holding its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) in a binary <c>Doc</c> property; a
/// single meta entity holds the monotonic generation a resolver caches against. The etag travels inside the
/// document, so optimistic concurrency is a read-compare-write. Works against Azure Storage and the Azurite emulator.
/// </summary>
public sealed class AzureStorageSecurityPolicyStore : ISecurityPolicyStore
{
    private const string RulesTable = "arazzoSecurityRules";
    private const string BindingsTable = "arazzoSecurityBindings";
    private const string MetaTable = "arazzoSecurityMeta";
    private const string RulePartition = "rule";
    private const string BindingPartition = "binding";
    private const string MetaPartition = "meta";
    private const string GenerationRowKey = "generation";

    // Singleton comparers (created once) for the client-side snapshot ordering, since Table queries are unordered:
    // rules by name, bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

    private readonly TableClient rules;
    private readonly TableClient bindings;
    private readonly TableClient meta;
    private readonly TimeProvider timeProvider;

    private AzureStorageSecurityPolicyStore(TableClient rules, TableClient bindings, TableClient meta, TimeProvider timeProvider)
    {
        this.rules = rules;
        this.bindings = bindings;
        this.meta = meta;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the policy tables over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the tables exist (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the policy tables over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the tables exist (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(RulesTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(BindingsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(MetaTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against already-provisioned tables.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSecurityPolicyStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageSecurityPolicyStore>(new AzureStorageSecurityPolicyStore(
            tableService.GetTableClient(RulesTable),
            tableService.GetTableClient(BindingsTable),
            tableService.GetTableClient(MetaTable),
            timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(RulePartition, Enc(name)) { ["Doc"] = json };
        try
        {
            await this.rules.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[]? doc = await DocumentAsync(this.rules, RulePartition, Enc(name), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<SecurityRuleDocument>(doc);
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => this.ReadRulesAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await DocumentAsync(this.rules, RulePartition, Enc(name), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(doc, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(RulePartition, Enc(name)) { ["Doc"] = json };
        await this.rules.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.rules, RulePartition, "rule", name, expectedEtag, static doc =>
        {
            using ParsedJsonDocument<SecurityRuleDocument> parsed = PersistedJson.ToPooledDocument<SecurityRuleDocument>(doc);
            return parsed.RootElement.EtagValue;
        }, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = json };
        await this.bindings.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc);
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => this.ReadBindingsAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(doc, "binding", id, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = json };
        await this.bindings.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.bindings, BindingPartition, "binding", id, expectedEtag, static doc =>
        {
            using ParsedJsonDocument<SecurityBindingDocument> parsed = PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc);
            return parsed.RootElement.EtagValue;
        }, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        PooledDocumentList<SecurityRuleDocument> ruleList = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindingList = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        NullableResponse<TableEntity> metaEntity = await this.meta.GetEntityIfExistsAsync<TableEntity>(MetaPartition, GenerationRowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        long generation = metaEntity.HasValue ? metaEntity.Value!.GetInt64("Generation") ?? 0 : 0;
        return new SecurityPolicySnapshot(ruleList, bindingList, generation);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static string Enc(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    private static async ValueTask<byte[]?> DocumentAsync(TableClient table, string partition, string rowKey, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> entity = await table.GetEntityIfExistsAsync<TableEntity>(partition, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        return entity.HasValue ? entity.Value!.GetBinary("Doc") : null;
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        await foreach (TableEntity entity in this.rules.QueryAsync<TableEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary("Doc") is { } bytes)
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(bytes));
            }
        }

        list.Sort(ByRuleName);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        await foreach (TableEntity entity in this.bindings.QueryAsync<TableEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary("Doc") is { } bytes)
            {
                list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(bytes));
            }
        }

        list.Sort(ByBindingOrder);
        return list;
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> metaEntity = await this.meta.GetEntityIfExistsAsync<TableEntity>(MetaPartition, GenerationRowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        long current = metaEntity.HasValue ? metaEntity.Value!.GetInt64("Generation") ?? 0 : 0;
        var entity = new TableEntity(MetaPartition, GenerationRowKey) { ["Generation"] = current + 1 };
        await this.meta.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> DeleteAsync(TableClient table, string partition, string kind, string key, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[]? doc = await DocumentAsync(table, partition, Enc(key), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, etagOf(doc));
        await table.DeleteEntityAsync(partition, Enc(key), ETag.All, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}