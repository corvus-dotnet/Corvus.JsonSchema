// <copyright file="AzureStorageSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
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

    // Separate §14.4 label tables for rules and bindings, because rule names and binding ids are independent id spaces
    // that could otherwise collide within a shared label partition.
    private const string RuleLabelsTable = "arazzoSecurityRuleLabels";
    private const string BindingLabelsTable = "arazzoSecurityBindingLabels";
    private const string RulePartition = "rule";
    private const string BindingPartition = "binding";
    private const string MetaPartition = "meta";
    private const string GenerationRowKey = "generation";

    // Singleton comparers (created once) for the client-side snapshot ordering, since Table queries are unordered:
    // rules by name, bindings by Order then id.
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

    private readonly TableClient rules;
    private readonly TableClient bindings;
    private readonly TableClient meta;
    private readonly TableClient ruleLabels;
    private readonly TableClient bindingLabels;
    private readonly AzureStorageSecurityLabelIndex ruleLabelIndex;
    private readonly AzureStorageSecurityLabelIndex bindingLabelIndex;
    private readonly TimeProvider timeProvider;

    private AzureStorageSecurityPolicyStore(TableClient rules, TableClient bindings, TableClient meta, TableClient ruleLabels, TableClient bindingLabels, TimeProvider timeProvider)
    {
        this.rules = rules;
        this.bindings = bindings;
        this.meta = meta;
        this.ruleLabels = ruleLabels;
        this.bindingLabels = bindingLabels;
        this.ruleLabelIndex = new AzureStorageSecurityLabelIndex(ruleLabels);
        this.bindingLabelIndex = new AzureStorageSecurityLabelIndex(bindingLabels);
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
        await tableService.GetTableClient(RuleLabelsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(BindingLabelsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
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
            tableService.GetTableClient(RuleLabelsTable),
            tableService.GetTableClient(BindingLabelsTable),
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
            ThrowHelper.ThrowSecurityRuleAlreadyExists(name);
        }

        // Mirror the management tags into the rule label table (RowKey = the row's encoded name) so the §14.2 reach resolves
        // to candidate rows server-side. Tags are immutable, so there is no re-point on update; an untagged, system-owned row
        // occupies no label partition and is admitted only under full read reach (fail-closed).
        await this.AddLabelsAsync(this.ruleLabels, Enc(name), draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[]? doc = await DocumentAsync(this.rules, RulePartition, Enc(name), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => this.ReadRulesAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        // Resolve the caller's read reach to candidate rows via the label index and load ONLY those (rather than the whole
        // partition) — the native reach benefit for a backend without server-side ordering. The narrowed, reach-admitted set
        // is then sorted, keyset-paged and q-filtered in memory by the shared pager (Table storage has no server-side order).
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(this.ruleLabelIndex, context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        using PooledDocumentList<SecurityRuleDocument> reachable = await this.LoadReachableRulesAsync(candidates, context, cancellationToken).ConfigureAwait(false);
        return SecurityRulePaging.PageInMemory(reachable, limit, pageToken, q);
    }

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
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(RulePartition, Enc(name)) { ["Doc"] = json };
        await this.rules.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.rules, RulePartition, "rule", name, this.ruleLabels, expectedEtag, static doc =>
        {
            using ParsedJsonDocument<SecurityRuleDocument> parsed = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
            return parsed.RootElement.EtagValue;
        }, static doc =>
        {
            using ParsedJsonDocument<SecurityRuleDocument> parsed = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
            return parsed.RootElement.ManagementTagsValue;
        }, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = json };
        await this.bindings.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);

        // Mirror the management tags into the binding label table (RowKey = the row's encoded id).
        await this.AddLabelsAsync(this.bindingLabels, Enc(id), draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => this.ReadBindingsAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        // Resolve the caller's read reach to candidate rows via the label index and load ONLY those, then keyset-page in memory.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(this.bindingLabelIndex, context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        using PooledDocumentList<SecurityBindingDocument> reachable = await this.LoadReachableBindingsAsync(candidates, context, cancellationToken).ConfigureAwait(false);
        return SecurityBindingPaging.PageInMemory(reachable, limit, pageToken, q);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, Enc(id), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(BindingPartition, Enc(id)) { ["Doc"] = json };
        await this.bindings.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.bindings, BindingPartition, "binding", id, this.bindingLabels, expectedEtag, static doc =>
        {
            using ParsedJsonDocument<SecurityBindingDocument> parsed = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
            return parsed.RootElement.EtagValue;
        }, static doc =>
        {
            using ParsedJsonDocument<SecurityBindingDocument> parsed = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
            return parsed.RootElement.ManagementTagsValue;
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
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

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
                list.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(bytes.AsMemory()));
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
                list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(bytes.AsMemory()));
            }
        }

        list.Sort(ByBindingOrder);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> LoadReachableRulesAsync(IReadOnlySet<string>? candidates, AccessContext context, CancellationToken cancellationToken)
    {
        // Full read reach (Admits(null, *) = true): the whole partition, already sorted by name.
        if (candidates is null)
        {
            return await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Reach-narrowed: point-read only the candidate rows, then apply the exact §14.2 reach (the label plan
        // over-approximates). An empty candidate set reads nothing.
        var list = new PooledDocumentList<SecurityRuleDocument>();
        try
        {
            foreach (string encRowKey in candidates)
            {
                byte[]? doc = await DocumentAsync(this.rules, RulePartition, encRowKey, cancellationToken).ConfigureAwait(false);
                if (doc is null)
                {
                    continue; // candidate but row gone — skip (a stale label is harmless)
                }

                ParsedJsonDocument<SecurityRuleDocument> document = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
                if (context.Admits(AccessVerb.Read, document.RootElement.ManagementTagsValue))
                {
                    list.Add(document);
                }
                else
                {
                    document.Dispose();
                }
            }

            list.Sort(ByRuleName);
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> LoadReachableBindingsAsync(IReadOnlySet<string>? candidates, AccessContext context, CancellationToken cancellationToken)
    {
        // Full read reach: the whole partition, already sorted by (order, id).
        if (candidates is null)
        {
            return await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        }

        // Reach-narrowed: point-read only the candidate rows, then apply the exact §14.2 reach.
        var list = new PooledDocumentList<SecurityBindingDocument>();
        try
        {
            foreach (string encRowKey in candidates)
            {
                byte[]? doc = await DocumentAsync(this.bindings, BindingPartition, encRowKey, cancellationToken).ConfigureAwait(false);
                if (doc is null)
                {
                    continue;
                }

                ParsedJsonDocument<SecurityBindingDocument> document = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
                if (context.Admits(AccessVerb.Read, document.RootElement.ManagementTagsValue))
                {
                    list.Add(document);
                }
                else
                {
                    document.Dispose();
                }
            }

            list.Sort(ByBindingOrder);
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> metaEntity = await this.meta.GetEntityIfExistsAsync<TableEntity>(MetaPartition, GenerationRowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        long current = metaEntity.HasValue ? metaEntity.Value!.GetInt64("Generation") ?? 0 : 0;
        var entity = new TableEntity(MetaPartition, GenerationRowKey) { ["Generation"] = current + 1 };
        await this.meta.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> DeleteAsync(TableClient table, string partition, string kind, string key, TableClient labels, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf, Func<byte[], SecurityTagSet> tagsOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[]? doc = await DocumentAsync(table, partition, Enc(key), cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, etagOf(doc));
        await table.DeleteEntityAsync(partition, Enc(key), ETag.All, cancellationToken).ConfigureAwait(false);

        // Tear down the row's label entries after the row is gone (a stale label is harmless by the §14.4 contract, a missing
        // one would hide a peer's row).
        await this.RemoveLabelsAsync(labels, Enc(key), tagsOf(doc), cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Upserts a row's label entries — one partition per tag token (two per tag), RowKey the row's encoded key. An untagged
    // row writes nothing. Idempotent: the same (token, rowKey) upsert repeats harmlessly.
    private async ValueTask AddLabelsAsync(TableClient labels, string encRowKey, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        foreach (string token in AzureStorageSecurityLabelIndex.TokensFor(managementTags))
        {
            await labels.UpsertEntityAsync(new TableEntity(token, encRowKey), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
    }

    // Deletes a row's label entries. DeleteEntityAsync tolerates a missing entry (404), so a concurrent teardown is safe.
    private async ValueTask RemoveLabelsAsync(TableClient labels, string encRowKey, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        foreach (string token in AzureStorageSecurityLabelIndex.TokensFor(managementTags))
        {
            await labels.DeleteEntityAsync(token, encRowKey, ETag.All, cancellationToken).ConfigureAwait(false);
        }
    }

    // Resolves the caller's read reach to candidate row keys (the encoded rule name / binding id) via the label index
    // (§14.4). Null = no narrowing (full read); empty = nothing admitted; otherwise a superset re-checked exactly with
    // context.Admits by the caller.
    private ValueTask<IReadOnlySet<string>?> ResolveReachCandidatesAsync(AzureStorageSecurityLabelIndex index, SecurityFilter? security, CancellationToken cancellationToken)
        => security is null
            ? new ValueTask<IReadOnlySet<string>?>((IReadOnlySet<string>?)null)
            : SecurityLabelQueryResolver.ResolveAsync(security.ToPredicate(SecurityLabelQueryEmitter.Instance), index, cancellationToken);
}