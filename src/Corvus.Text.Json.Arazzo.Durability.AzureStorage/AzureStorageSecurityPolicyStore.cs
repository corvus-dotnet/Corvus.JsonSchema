// <copyright file="AzureStorageSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
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
    public async ValueTask<SecurityRuleRecord> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        var record = new SecurityRuleRecord(name, definition.Expression, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        var entity = new TableEntity(RulePartition, Enc(name)) { ["Doc"] = SecurityRuleDocument.From(record).ToJsonBytes() };
        try
        {
            await this.rules.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[]? doc = await DocumentAsync(this.rules, RulePartition, Enc(name), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityRuleDocument.FromJson(doc).ToRecord();
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
        byte[]? doc = await DocumentAsync(this.rules, RulePartition, Enc(name), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityRuleRecord current = SecurityRuleDocument.FromJson(doc).ToRecord();
        EnsureEtag("rule", name, expectedEtag, current.Etag);
        var updated = current with
        {
            Expression = definition.Expression,
            Description = definition.Description,
            UpdatedBy = actor,
            UpdatedAt = this.timeProvider.GetUtcNow(),
            Etag = NewEtag(),
        };
        var entity = new TableEntity(RulePartition, Enc(name)) { ["Doc"] = SecurityRuleDocument.From(updated).ToJsonBytes() };
        await this.rules.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.rules, RulePartition, "rule", name, expectedEtag, doc => SecurityRuleDocument.FromJson(doc).ToRecord().Etag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        var record = new SecurityBinding(id, definition.ClaimType, definition.ClaimValue, definition.Read, definition.Write, definition.Purge, definition.Order, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = SecurityBindingDocument.From(record).ToJsonBytes() };
        await this.bindings.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityBindingDocument.FromJson(doc).ToRecord();
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
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityBinding current = SecurityBindingDocument.FromJson(doc).ToRecord();
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
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = SecurityBindingDocument.From(updated).ToJsonBytes() };
        await this.bindings.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.bindings, BindingPartition, "binding", id, expectedEtag, doc => SecurityBindingDocument.FromJson(doc).ToRecord().Etag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SecurityRuleRecord> ruleList = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityBinding> bindingList = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        NullableResponse<TableEntity> metaEntity = await this.meta.GetEntityIfExistsAsync<TableEntity>(MetaPartition, GenerationRowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        long generation = metaEntity.HasValue ? metaEntity.Value!.GetInt64("Generation") ?? 0 : 0;
        return new SecurityPolicySnapshot(ruleList, bindingList, generation);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static string Enc(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private static async ValueTask<byte[]?> DocumentAsync(TableClient table, string partition, string rowKey, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> entity = await table.GetEntityIfExistsAsync<TableEntity>(partition, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        return entity.HasValue ? entity.Value!.GetBinary("Doc") : null;
    }

    private async ValueTask<IReadOnlyList<SecurityRuleRecord>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new List<SecurityRuleRecord>();
        await foreach (TableEntity entity in this.rules.QueryAsync<TableEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary("Doc") is { } bytes)
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
        await foreach (TableEntity entity in this.bindings.QueryAsync<TableEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary("Doc") is { } bytes)
            {
                list.Add(SecurityBindingDocument.FromJson(bytes).ToRecord());
            }
        }

        list.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
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

        EnsureEtag(kind, key, expectedEtag, etagOf(doc));
        await table.DeleteEntityAsync(partition, Enc(key), ETag.All, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}