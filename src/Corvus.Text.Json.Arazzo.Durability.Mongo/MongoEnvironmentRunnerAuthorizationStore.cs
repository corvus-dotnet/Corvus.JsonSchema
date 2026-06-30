// <copyright file="MongoEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a deployment
/// environment (design §5.5): its decision state and audit metadata. Each authorization is a document keyed by the composite
/// <c>(environment, runnerId)</c>, holding its <see cref="EnvironmentRunnerAuthorization"/> Corvus.Text.Json schema document as
/// a binary <c>doc</c> field; the filterable fields (environment, runnerId, status) are mirrored into BSON fields so the list
/// query filters and the keyset page orders by <c>(environment, runnerId)</c> server-side. The etag travels inside the
/// document, so optimistic concurrency is a read-compare-write. Mirrors <see cref="MongoAvailabilityRequestStore"/>, keyed by
/// environment + runner rather than a single id.
/// </summary>
/// <remarks>
/// The two key parts cannot be concatenated into a string id (an environment name or a runner id can carry any character,
/// including a separator), so — like <c>MongoSourceCredentialStore</c> — the composite <c>_id</c> is a structured subdocument
/// (<c>{ e, r }</c>). The unique <c>_id</c> alone enforces the <c>(environment, runnerId)</c> uniqueness that makes the insert
/// idempotent, so the operational user needs only <c>readWrite</c>; the driver pools connections internally, so the store is
/// naturally concurrent.
/// </remarks>
public sealed class MongoEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    // (Environment, RunnerId) ascending — the list/keyset order. BSON compares strings by bytes, so this is ordinal byte
    // order, the same total order the in-memory pager's span compare and the keyset predicate use.
    private static readonly SortDefinition<BsonDocument> ByEnvironmentThenRunner =
        Builders<BsonDocument>.Sort.Ascending("environment").Ascending("runnerId");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> authorizations;

    private MongoEnvironmentRunnerAuthorizationStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.authorizations = database.GetCollection<BsonDocument>("runner_authorizations");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoEnvironmentRunnerAuthorizationStore>(new MongoEnvironmentRunnerAuthorizationStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoEnvironmentRunnerAuthorizationStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoEnvironmentRunnerAuthorizationStore>(new MongoEnvironmentRunnerAuthorizationStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending).
        byte[]? existing = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
        var document = new BsonDocument
        {
            ["_id"] = Key(environment, runnerId),
            ["environment"] = environment,
            ["runnerId"] = runnerId,
            ["status"] = RunnerAuthorizationStatusNames.Pending,
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.authorizations.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent ensure-pending won the race; the authorization now exists, so honour idempotency.
            byte[]? raced = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
            if (raced is not null)
            {
                return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(raced);
            }

            throw;
        }

        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        List<BsonDocument> documents = await this.authorizations.Find(filter).Sort(ByEnvironmentThenRunner).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(document["doc"].AsBsonBinaryData.Bytes));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the Mongo filter needs (the leaf) only here.
        // Undefined token = first page; a malformed token throws FormatException.
        string? cursorEnvironment = null;
        string? cursorRunnerId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> cursorEnvUtf8, out ReadOnlySpan<byte> cursorRunnerUtf8))
                {
                    cursorEnvironment = Encoding.UTF8.GetString(cursorEnvUtf8);
                    cursorRunnerId = Encoding.UTF8.GetString(cursorRunnerUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        if (cursorEnvironment is not null)
        {
            // Keyset seek strictly past (environment, runnerId): BSON compares strings by bytes, so this Or of the compound
            // conditions is the same total order ByEnvironmentThenRunner / the in-memory pager uses.
            filter &= b.Or(
                b.Gt("environment", cursorEnvironment),
                b.And(b.Eq("environment", cursorEnvironment), b.Gt("runnerId", cursorRunnerId)));
        }

        // The (environment, runnerId) sort + Limit(n+1) bounds the read to one page + 1 (lookahead) — never the whole queue.
        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            List<BsonDocument> documents = await this.authorizations
                .Find(filter)
                .Sort(ByEnvironmentThenRunner)
                .Limit(pageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = documents.Count > pageSize;
            int take = hasMore ? pageSize : documents.Count;
            for (int i = 0; i < take; i++)
            {
                page.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(documents[i]["doc"].AsBsonBinaryData.Bytes));
            }

            if (!hasMore)
            {
                return EnvironmentRunnerAuthorizationPage.Create(page);
            }

            EnvironmentRunnerAuthorization last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastEnv = last.Environment.GetUtf8String();
            using UnescapedUtf8JsonString lastRunner = last.RunnerId.GetUtf8String();
            return EnvironmentRunnerAuthorizationPage.Create(page, lastEnv.Span, lastRunner.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc);
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("status", RunnerAuthorizationStatusNames.ToWire(decision.Status))
            .Set("doc", new BsonBinaryData(json));
        await this.authorizations.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Key(environment, runnerId)), update, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
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

    // The composite document id: (environment, runnerId). Both can carry arbitrary text (including any separator control
    // char), so the parts are held as a structured subdocument rather than concatenated into a string id — the unique _id
    // then rejects a duplicate (environment, runnerId) on insert (idempotency).
    private static BsonDocument Key(string environment, string runnerId)
        => new()
        {
            ["e"] = environment,
            ["r"] = runnerId,
        };

    // Builds the shared list filter (status / environment) plus the approver-inbox filter (the authorization's environment
    // must be one the caller administers — server-derived strings). The administered set is never empty here (the handler
    // short-circuits a caller who administers nothing to an empty page before the store), but a null/empty set (the non-inbox
    // modes) adds nothing.
    private static FilterDefinition<BsonDocument> BuildFilter(RunnerAuthorizationQuery query)
    {
        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (query.Status is { } status)
        {
            filter &= b.Eq("status", RunnerAuthorizationStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            filter &= b.Eq("environment", environment);
        }

        if (query.AdministeredEnvironments is { Count: > 0 } administered)
        {
            filter &= b.In("environment", administered);
        }

        return filter;
    }

    private async ValueTask<byte[]?> DocumentAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.authorizations.Find(Builders<BsonDocument>.Filter.Eq("_id", Key(environment, runnerId))).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}