// <copyright file="MongoSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2). Each record is a document keyed by name/id holding its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) as a binary field; a single-document
/// meta collection holds the monotonic generation a resolver caches against. The etag travels inside the document,
/// so optimistic concurrency is a read-compare-write.
/// </summary>
public sealed class MongoSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private const string MetaId = "meta";

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> rules;
    private readonly IMongoCollection<BsonDocument> bindings;
    private readonly IMongoCollection<BsonDocument> meta;

    private MongoSecurityPolicyStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.rules = database.GetCollection<BsonDocument>("security_rules");
        this.bindings = database.GetCollection<BsonDocument>("security_bindings");
        this.meta = database.GetCollection<BsonDocument>("security_meta");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoSecurityPolicyStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoSecurityPolicyStore>(new MongoSecurityPolicyStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoSecurityPolicyStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoSecurityPolicyStore>(new MongoSecurityPolicyStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        var record = SecurityRuleDocument.CreateRule(name, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var document = new BsonDocument { ["_id"] = name, ["doc"] = new BsonBinaryData(record.ToJsonBytes()) };
        try
        {
            await this.rules.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[]? doc = await DocumentAsync(this.rules, name, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityRuleDocument.FromJson(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await DocumentAsync(this.rules, name, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityRuleDocument current = SecurityRuleDocument.FromJson(doc);
        EnsureEtag("rule", name, expectedEtag, current.EtagValue);
        SecurityRuleDocument updated = current.WithUpdate(definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var replacement = new BsonDocument { ["_id"] = name, ["doc"] = new BsonBinaryData(updated.ToJsonBytes()) };
        await this.rules.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", name), replacement, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.rules, "rule", name, expectedEtag, doc => SecurityRuleDocument.FromJson(doc).EtagValue, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        var record = SecurityBindingDocument.CreateBinding(id, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var document = new BsonDocument { ["_id"] = id, ["doc"] = new BsonBinaryData(record.ToJsonBytes()) };
        await this.bindings.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await DocumentAsync(this.bindings, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityBindingDocument.FromJson(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await DocumentAsync(this.bindings, id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityBindingDocument current = SecurityBindingDocument.FromJson(doc);
        EnsureEtag("binding", id, expectedEtag, current.EtagValue);
        SecurityBindingDocument updated = current.WithUpdate(definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var replacement = new BsonDocument { ["_id"] = id, ["doc"] = new BsonBinaryData(updated.ToJsonBytes()) };
        await this.bindings.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), replacement, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.bindings, "binding", id, expectedEtag, doc => SecurityBindingDocument.FromJson(doc).EtagValue, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SecurityRuleDocument> ruleList = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityBindingDocument> bindingList = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        BsonDocument? metaDoc = await this.meta.Find(Builders<BsonDocument>.Filter.Eq("_id", MetaId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        long generation = metaDoc is not null && metaDoc.TryGetValue("generation", out BsonValue value) ? value.ToInt64() : 0;
        return new SecurityPolicySnapshot(ruleList, bindingList, generation);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private static async ValueTask<byte[]?> DocumentAsync(IMongoCollection<BsonDocument> collection, string key, CancellationToken cancellationToken)
    {
        BsonDocument? document = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", key)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }

    private async ValueTask<IReadOnlyList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new List<SecurityRuleDocument>();
        List<BsonDocument> documents = await this.rules.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(SecurityRuleDocument.FromJson(document["doc"].AsBsonBinaryData.Bytes));
        }

        list.Sort(static (a, b) => string.CompareOrdinal(a.NameValue, b.NameValue));
        return list;
    }

    private async ValueTask<IReadOnlyList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new List<SecurityBindingDocument>();
        List<BsonDocument> documents = await this.bindings.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(SecurityBindingDocument.FromJson(document["doc"].AsBsonBinaryData.Bytes));
        }

        list.Sort(static (a, b) => a.OrderValue != b.OrderValue ? a.OrderValue.CompareTo(b.OrderValue) : string.CompareOrdinal(a.IdValue, b.IdValue));
        return list;
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        await this.meta.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", MetaId),
            Builders<BsonDocument>.Update.Inc("generation", 1L),
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> DeleteAsync(IMongoCollection<BsonDocument> collection, string kind, string key, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[]? doc = await DocumentAsync(collection, key, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return false;
        }

        EnsureEtag(kind, key, expectedEtag, etagOf(doc));
        await collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", key), cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}