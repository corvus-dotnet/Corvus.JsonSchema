// <copyright file="CosmosSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules +
/// claim→rule bindings, design §14.2). Each record is one document in a small <c>workflow_security</c> container,
/// partitioned by record kind, holding its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) as a base64 field; a single meta
/// document holds the monotonic generation a resolver caches against. Documents are written and read through the
/// Cosmos <em>stream</em> APIs (no SDK serializer); the etag travels inside the document, so optimistic concurrency
/// is a read-compare-write.
/// </summary>
public sealed class CosmosSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_security";
    private const string RulePartition = "rule";
    private const string BindingPartition = "binding";
    private const string MetaPartition = "meta";
    private const string MetaId = "meta";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();
    private static readonly byte[] GenerationProperty = "generation"u8.ToArray();

    // Singleton comparers (created once) for the client-side snapshot ordering, since the queries do not order: rules by
    // their name and bindings by Order then id.
    private static readonly IComparer<ParsedJsonDocument<SecurityRuleDocument>> ByRuleName =
        Comparer<ParsedJsonDocument<SecurityRuleDocument>>.Create(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));

    private static readonly IComparer<ParsedJsonDocument<SecurityBindingDocument>> ByBindingOrder =
        Comparer<ParsedJsonDocument<SecurityBindingDocument>>.Create(static (a, b) => a.RootElement.OrderValue != b.RootElement.OrderValue ? a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosSecurityPolicyStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.container = container;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
    }

    /// <summary>The Cosmos client options the store relies on (none; it uses the stream APIs + Corvus.Text.Json).</summary>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

    /// <summary>Provisions the store's database and container over the given connection string.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's database and container over a caller-supplied client.</summary>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static ValueTask PrepareAsync(CosmosClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the store for operation against an already-provisioned database and container.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosSecurityPolicyStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosSecurityPolicyStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosSecurityPolicyStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosSecurityPolicyStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        using Stream stream = EnvelopeStream<SecurityRuleDocument, (string Name, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            name,
            RulePartition,
            (name, draft, actor, this.timeProvider.GetUtcNow(), NewEtag()),
            static (Utf8JsonWriter writer, in (string Name, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityRuleDocument.WriteNew(writer, c.Name, c.Draft, c.Actor, c.At, c.Tag),
            out ParsedJsonDocument<SecurityRuleDocument> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(RulePartition), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"A security rule named '{name}' already exists.");
            }

            response.EnsureSuccessStatusCode();
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[]? doc = await this.DocumentAsync(name, RulePartition, cancellationToken).ConfigureAwait(false);
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
        byte[]? doc = await this.DocumentAsync(name, RulePartition, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        // Parse the existing document once (pooled) to check the etag and carry its immutable audit fields forward; the
        // updated document is serialized straight into the envelope (no owned byte[]).
        using ParsedJsonDocument<SecurityRuleDocument> current = PersistedJson.ToPooledDocument<SecurityRuleDocument>(doc);
        SecurityPolicySerialization.EnsureEtag("rule", name, expectedEtag, current.RootElement.EtagValue);
        using Stream stream = EnvelopeStream<SecurityRuleDocument, (SecurityRuleDocument Cur, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            name,
            RulePartition,
            (current.RootElement, draft, actor, this.timeProvider.GetUtcNow(), NewEtag()),
            static (Utf8JsonWriter writer, in (SecurityRuleDocument Cur, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Cur.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag),
            out ParsedJsonDocument<SecurityRuleDocument> document);
        try
        {
            using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, name, new PartitionKey(RulePartition), cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(name, RulePartition, "rule", expectedEtag, SecurityPolicySerialization.RuleEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        using Stream stream = EnvelopeStream<SecurityBindingDocument, (string Id, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            id,
            BindingPartition,
            (id, draft, actor, this.timeProvider.GetUtcNow(), NewEtag()),
            static (Utf8JsonWriter writer, in (string Id, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityBindingDocument.WriteNew(writer, c.Id, c.Draft, c.Actor, c.At, c.Tag),
            out ParsedJsonDocument<SecurityBindingDocument> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(BindingPartition), cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, BindingPartition, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc);
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
        => this.ReadBindingsAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(id, BindingPartition, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        // Parse the existing document once (pooled) to check the etag and carry its immutable audit fields forward; the
        // updated document is serialized straight into the envelope (no owned byte[]).
        using ParsedJsonDocument<SecurityBindingDocument> current = PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc);
        SecurityPolicySerialization.EnsureEtag("binding", id, expectedEtag, current.RootElement.EtagValue);
        using Stream stream = EnvelopeStream<SecurityBindingDocument, (SecurityBindingDocument Cur, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            id,
            BindingPartition,
            (current.RootElement, draft, actor, this.timeProvider.GetUtcNow(), NewEtag()),
            static (Utf8JsonWriter writer, in (SecurityBindingDocument Cur, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Cur.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag),
            out ParsedJsonDocument<SecurityBindingDocument> document);
        try
        {
            using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, id, new PartitionKey(BindingPartition), cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync(id, BindingPartition, "binding", expectedEtag, SecurityPolicySerialization.BindingEtagOf, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        PooledDocumentList<SecurityRuleDocument> rules = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindings = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        long generation = await this.ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
        return new SecurityPolicySnapshot(rules, bindings, generation);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Serializes the rule/binding ONCE into a pooled buffer (CosmosJson.RentJson), builds the caller's pooled return
    // document from it, then writes the {id, pk, doc:base64} envelope into a pooled stream (CosmosJson.WriteToStream),
    // base64-ing the same bytes. No owned byte[] and no nested writer rent: the doc lives only in the pooled rent
    // (transient) and the returned document's pooled array (caller-disposed); the envelope stream is pooled too. On any
    // failure building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream<T, TContext>(
        string id,
        string partition,
        in TContext context,
        PersistedJson.WriteCallback<TContext> writeDocument,
        out ParsedJsonDocument<T> document)
        where T : struct, IJsonElement<T>
    {
        using CosmosJson.RentedJson docJson = CosmosJson.RentJson(in context, writeDocument);
        document = PersistedJson.ToPooledDocument<T>(docJson.Span);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Partition: partition, Doc: docJson),
                static (Utf8JsonWriter writer, in (string Id, string Partition, CosmosJson.RentedJson Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Partition);

                    // The policy document is JSON — embed it verbatim as a nested value, not base64 (which would be a
                    // spurious encode here + decode on read). It is valid JSON we produced, so skip validation.
                    writer.WritePropertyName("doc"u8);
                    writer.WriteRawValue(c.Doc.Span, skipInputValidation: true);
                    writer.WriteEndObject();
                });
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosSecurityPolicyStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosSecurityPolicyStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    private async ValueTask<byte[]?> DocumentAsync(string id, string partition, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(id, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);

        // The embedded doc is raw nested JSON (no base64). Copy it out — it outlives the pooled response page.
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(payload.Memory, DocProperty);
        return doc.IsEmpty ? null : doc.ToArray();
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(RulePartition, cancellationToken).ConfigureAwait(false))
        {
            list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(doc.Span));
        }

        list.Sort(ByRuleName);
        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(BindingPartition, cancellationToken).ConfigureAwait(false))
        {
            list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc.Span));
        }

        list.Sort(ByBindingOrder);
        return list;
    }

    // Yields each embedded policy document's raw UTF-8 bytes (a slice into the pooled response page) — no base64
    // decode. The consumer copies it (ToPooledDocument into the list) within the iteration step, before the page rolls.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(string partition, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.doc FROM c WHERE c.pk = @pk").WithParameter("@pk", partition);
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(
            query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partition) });
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(element, DocProperty);
                if (!doc.IsEmpty)
                {
                    yield return doc;
                }
            }
        }
    }

    private async ValueTask<long> ReadGenerationAsync(CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(MetaId, new PartitionKey(MetaPartition), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return 0;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        return CosmosJson.GetInt64(payload.Memory, GenerationProperty) ?? 0;
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        long next = await this.ReadGenerationAsync(cancellationToken).ConfigureAwait(false) + 1;
        using Stream stream = CosmosJson.WriteToStream(
            next,
            static (Utf8JsonWriter writer, in long generation) =>
            {
                writer.WriteStartObject();
                writer.WriteString("id"u8, MetaId);
                writer.WriteString("pk"u8, MetaPartition);
                writer.WriteNumber("generation"u8, generation);
                writer.WriteEndObject();
            });
        using ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(MetaPartition), cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async ValueTask<bool> DeleteAsync(string id, string partition, string kind, WorkflowEtag expectedEtag, Func<byte[], WorkflowEtag> etagOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, partition, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, id, expectedEtag, etagOf(doc));
        using ResponseMessage response = await this.container.DeleteItemStreamAsync(id, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}