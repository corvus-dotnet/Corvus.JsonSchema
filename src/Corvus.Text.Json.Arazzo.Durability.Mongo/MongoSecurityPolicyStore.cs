// <copyright file="MongoSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
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

    // Singleton comparers (created once) for the client-side snapshot ordering, since the queries do not order: rules by
    // their name and bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

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
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);

        // Mirror the q-searched expression top-level (the name is _id) so q runs server-side; _id is the keyset.
        var document = new BsonDocument { ["_id"] = name, ["expression"] = draft.ExpressionValue, ["doc"] = new BsonBinaryData(json) };
        try
        {
            await this.rules.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
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
        byte[]? doc = await DocumentAsync(this.rules, name, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
        => this.ReadRulesAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;
        string? after = DecodeRuleCursor(pageToken);

        // _id is the name (the keyset); BSON compares strings by bytes (ordinal == the in-memory pager). q matches the name
        // (_id) or the mirrored expression field via a case-insensitive literal-substring regex.
        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (after is not null)
        {
            filter &= b.Gt("_id", after);
        }

        if (q.IsNotUndefined())
        {
            var rx = new BsonRegularExpression(Regex.Escape((string)q), "i");
            filter &= b.Or(b.Regex("_id", rx), b.Regex("expression", rx));
        }

        var page = new PooledDocumentList<SecurityRuleDocument>(pageSize);
        try
        {
            List<BsonDocument> documents = await this.rules
                .Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                .Limit(pageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = documents.Count > pageSize;
            int take = hasMore ? pageSize : documents.Count;
            for (int i = 0; i < take; i++)
            {
                page.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(documents[i]["doc"].AsBsonBinaryData.Bytes.AsMemory()));
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
        byte[]? doc = await DocumentAsync(this.rules, name, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        var replacement = new BsonDocument { ["_id"] = name, ["expression"] = draft.ExpressionValue, ["doc"] = new BsonBinaryData(json) };
        await this.rules.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", name), replacement, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.rules, "rule", name, expectedEtag, SecurityPolicySerialization.RuleEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        BsonDocument document = BindingDocument(id, draft, json);
        await this.bindings.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await DocumentAsync(this.bindings, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => this.ReadBindingsAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);

        // Keyset (order, _id): the mirrored order field then _id (BSON byte order == the in-memory pager). q matches the
        // mirrored claimType/claimValue/description via a case-insensitive literal-substring regex.
        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (hasCursor)
        {
            filter &= b.Or(
                b.Gt("order", cursorOrder),
                b.And(b.Eq("order", cursorOrder), b.Gt("_id", cursorId)));
        }

        if (q.IsNotUndefined())
        {
            var rx = new BsonRegularExpression(Regex.Escape((string)q), "i");
            filter &= b.Or(b.Regex("claimType", rx), b.Regex("claimValue", rx), b.Regex("description", rx));
        }

        var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
        try
        {
            List<BsonDocument> documents = await this.bindings
                .Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Ascending("order").Ascending("_id"))
                .Limit(pageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = documents.Count > pageSize;
            int take = hasMore ? pageSize : documents.Count;
            for (int i = 0; i < take; i++)
            {
                page.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(documents[i]["doc"].AsBsonBinaryData.Bytes.AsMemory()));
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
        byte[]? doc = await DocumentAsync(this.bindings, id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
        byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        BsonDocument replacement = BindingDocument(id, draft, json);
        await this.bindings.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), replacement, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(this.bindings, "binding", id, expectedEtag, SecurityPolicySerialization.BindingEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        PooledDocumentList<SecurityRuleDocument> ruleList = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindingList = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
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

    // The stored binding document: _id + the canonical doc, plus the mirrored keyset/q fields (order, claimType, and the
    // optional claimValue/description when present) so the keyset and q run server-side.
    private static BsonDocument BindingDocument(string id, SecurityBindingDocument draft, byte[] json)
    {
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["order"] = draft.OrderValue,
            ["claimType"] = draft.ClaimTypeValue,
            ["doc"] = new BsonBinaryData(json),
        };
        if (draft.ClaimValue.IsNotUndefined())
        {
            document["claimValue"] = (string)draft.ClaimValue;
        }

        if (draft.Description.IsNotUndefined())
        {
            document["description"] = (string)draft.Description;
        }

        return document;
    }

    // Decodes the keyset cursor (rule name) from the request page token; the name reified to a string only for the _id
    // filter (the leaf). Undefined = first page.
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

    // Decodes the keyset cursor (order, id) from the request page token; the id reified to a string only for the _id filter.
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

    private static async ValueTask<byte[]?> DocumentAsync(IMongoCollection<BsonDocument> collection, string key, CancellationToken cancellationToken)
    {
        BsonDocument? document = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", key)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        List<BsonDocument> documents = await this.rules.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(document["doc"].AsBsonBinaryData.Bytes.AsMemory()));
        }

        list.Sort(ByRuleName);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        List<BsonDocument> documents = await this.bindings.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(document["doc"].AsBsonBinaryData.Bytes.AsMemory()));
        }

        list.Sort(ByBindingOrder);
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

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, etagOf(doc));
        await collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", key), cancellationToken).ConfigureAwait(false);
        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}