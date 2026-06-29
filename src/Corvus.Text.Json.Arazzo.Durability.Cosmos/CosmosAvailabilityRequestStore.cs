// <copyright file="CosmosAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Internal;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IAvailabilityRequestStore"/> — availability ("promotion") requests (design §7.8): a
/// principal's request to make a workflow version available in an environment, its decision state, and audit metadata. Each
/// request is one document in a small <c>availability_requests</c> container, partitioned by — and identified within that
/// partition by — its own id, so a request is a single logical record reached by a single-partition point read. The record
/// holds its <see cref="AvailabilityRequest"/> schema document verbatim as a raw nested JSON value (no base64 round-trip);
/// the filterable fields (status, target environment, requester, creation instant) are mirrored to top-level envelope
/// properties so <see cref="ListAsync(AvailabilityRequestQuery, CancellationToken)"/> can query and order on them, and the
/// store-owned etag travels inside the embedded document. Documents are written and read through the Cosmos <em>stream</em>
/// APIs (no SDK serializer), so persistence flows through Corvus.Text.Json. Mirrors <see cref="CosmosAccessRequestStore"/>,
/// parameterised by environment.
/// </summary>
/// <remarks>
/// Read/return methods hand back pooled documents whose lifetime the caller owns. <see cref="DecideAsync"/> reads the
/// current document, checks its embedded etag (through <see cref="AvailabilityRequestSerialization.SerializeDecision"/>) and
/// replaces it by its id — mirroring the other backends' optimistic concurrency, so two administrators cannot
/// double-decide the same request.
/// </remarks>
public sealed class CosmosAvailabilityRequestStore : IAvailabilityRequestStore, IAsyncDisposable
{
    private const string ContainerId = "availability_requests";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosAvailabilityRequestStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosAvailabilityRequestStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosAvailabilityRequestStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosAvailabilityRequestStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosAvailabilityRequestStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "areq-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AvailabilityRequestSerialization.SerializeNew(id, draft, actor, now, NewEtag());
        var fields = new EnvelopeFields(
            id,
            draft.EnvironmentValue,
            actor,
            AvailabilityRequestStatusNames.Pending,
            now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            json);
        using Stream stream = EnvelopeStream(in fields, out ParsedJsonDocument<AvailabilityRequest> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        using CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(id, cancellationToken).ConfigureAwait(false);
        if (payload is not { } page)
        {
            return null;
        }

        // The embedded doc is raw nested JSON (a slice of the live pooled response). Copy it into an owned pooled
        // document (no GC array) before the response buffer is returned at the end of this using.
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
        return doc.IsEmpty ? null : PersistedJson.ToPooledDocument<AvailabilityRequest>(doc.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        // A cross-partition query over the mirrored top-level filterable fields, oldest-first by creation instant then
        // id (the id tiebreak makes the order total). The embedded doc is the only projection — each request's etag and
        // audit fields live inside it. Every criterion binds as a parameter (no concatenation).
        var conditions = new List<string>(4);
        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.Environment is not null)
        {
            conditions.Add("c.env = @env");
        }

        if (query.CreatedBy is not null)
        {
            conditions.Add("c.createdBy = @by");
        }

        string[]? adminNames = AppendAdministeredCondition(conditions, query.AdministeredEnvironments);

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.createdAt, c.id");
        if (query.Status is { } statusFilter)
        {
            definition = definition.WithParameter("@status", AvailabilityRequestStatusNames.ToWire(statusFilter));
        }

        if (query.Environment is { } environment)
        {
            definition = definition.WithParameter("@env", environment);
        }

        if (query.CreatedBy is { } createdBy)
        {
            definition = definition.WithParameter("@by", createdBy);
        }

        definition = WithAdministeredParameters(definition, query.AdministeredEnvironments, adminNames);

        var list = new PooledDocumentList<AvailabilityRequest>();
        try
        {
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
            {
                list.Add(PersistedJson.ToPooledDocument<AvailabilityRequest>(doc.Span));
            }

            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    // This project generates its own Cosmos-namespace JsonString (per-root type identity), so the seam parameter is fully
    // qualified to the core JsonString IAvailabilityRequestStore's signature uses.
    public async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, global::Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AvailabilityRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Cosmos parameters need (the leaf) only here —
        // createdAt as the ISO-8601 "o" form the mirrored c.createdAt stores (reconstructed from the token's UTC ticks).
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AvailabilityRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AvailabilityRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        var conditions = new List<string>(5);
        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.Environment is not null)
        {
            conditions.Add("c.env = @env");
        }

        if (query.CreatedBy is not null)
        {
            conditions.Add("c.createdBy = @by");
        }

        string[]? adminNames = AppendAdministeredCondition(conditions, query.AdministeredEnvironments);

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): Cosmos orders strings ordinally, so the ISO createdAt order is
            // chronological and the id order is byte-ordinal — the same total order the in-memory pager uses.
            conditions.Add("(c.createdAt > @ca OR (c.createdAt = @ca AND c.id > @id))");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.createdAt, c.id");
        if (query.Status is { } statusFilter)
        {
            definition = definition.WithParameter("@status", AvailabilityRequestStatusNames.ToWire(statusFilter));
        }

        if (query.Environment is { } environment)
        {
            definition = definition.WithParameter("@env", environment);
        }

        if (query.CreatedBy is { } createdBy)
        {
            definition = definition.WithParameter("@by", createdBy);
        }

        if (cursorCreatedAt is not null)
        {
            definition = definition.WithParameter("@ca", cursorCreatedAt).WithParameter("@id", cursorId);
        }

        definition = WithAdministeredParameters(definition, query.AdministeredEnvironments, adminNames);

        var page = new PooledDocumentList<AvailabilityRequest>(pageSize);
        try
        {
            bool hasMore = false;
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
            {
                if (page.Count == pageSize)
                {
                    hasMore = true; // a row beyond the page exists → there is a next page; stop early
                    break;
                }

                page.Add(PersistedJson.ToPooledDocument<AvailabilityRequest>(doc.Span));
            }

            if (!hasMore)
            {
                return AvailabilityRequestPage.Create(page);
            }

            AvailabilityRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AvailabilityRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ParsedJsonDocument<AvailabilityRequest> document;
        Stream stream;
        using (CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (payload is not { } page)
            {
                return null;
            }

            ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
            if (doc.IsEmpty)
            {
                return null;
            }

            // Parse the existing document NON-COPYING over the live response (no GC array, no pooled copy) to check the
            // etag and carry its immutable fields forward; a stale etag throws AvailabilityRequestConflictException. The
            // status mirror moves to the decided value; the environment/requester/creation mirrors are immutable, so they
            // carry through from the stored record (read off the same parsed model, like the other backends). The parse and
            // its synchronous consumers (SerializeDecision, the mirror reads, EnvelopeStream) all complete before the
            // response buffer is returned at the end of this using.
            using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(doc);
            byte[] json = AvailabilityRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), NewEtag());
            var fields = new EnvelopeFields(
                id,
                current.RootElement.EnvironmentValue,
                current.RootElement.CreatedByValue,
                AvailabilityRequestStatusNames.ToWire(decision.Status),
                current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                json);
            stream = EnvelopeStream(in fields, out document);
        }

        using (stream)
        {
            try
            {
                using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }
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

    // The approver-inbox filter (design §7.8): adds "c.env IN (@adm0, ...)" to the conditions and returns the parameter
    // names so the caller can bind the (server-derived) administered environments. The set is never empty here (the handler
    // short-circuits a caller who administers nothing); a null set (the non-inbox modes) adds nothing and returns null.
    private static string[]? AppendAdministeredCondition(List<string> conditions, IReadOnlyList<string>? administered)
    {
        if (administered is not { Count: > 0 } set)
        {
            return null;
        }

        var names = new string[set.Count];
        for (int i = 0; i < set.Count; i++)
        {
            names[i] = "@adm" + i.ToString(CultureInfo.InvariantCulture);
        }

        conditions.Add("c.env IN (" + string.Join(", ", names) + ")");
        return names;
    }

    // Binds the administered-environment parameters produced by AppendAdministeredCondition onto the query definition.
    private static QueryDefinition WithAdministeredParameters(QueryDefinition definition, IReadOnlyList<string>? administered, string[]? names)
    {
        if (names is null || administered is null)
        {
            return definition;
        }

        for (int i = 0; i < names.Length; i++)
        {
            definition = definition.WithParameter(names[i], administered[i]);
        }

        return definition;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Serializes the {id, pk, env, by, status, createdAt, doc} envelope into a pooled stream and builds the caller's pooled
    // return document from the same request bytes. The request document is itself JSON, so embed it verbatim as a nested
    // value — no base64 wrap (which would be a spurious encode here + decode on read). It is valid JSON we produced, so skip
    // validation. On any failure building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(in EnvelopeFields fields, out ParsedJsonDocument<AvailabilityRequest> document)
    {
        document = PersistedJson.ToPooledDocument<AvailabilityRequest>(fields.Doc);
        try
        {
            return CosmosJson.WriteToStream(
                in fields,
                static (Utf8JsonWriter writer, in EnvelopeFields c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);
                    writer.WriteString("env"u8, c.Environment);
                    writer.WriteString("createdBy"u8, c.CreatedBy);
                    writer.WriteString("status"u8, c.Status);
                    writer.WriteString("createdAt"u8, c.CreatedAt);
                    writer.WritePropertyName("doc"u8);
                    writer.WriteRawValue(c.Doc, skipInputValidation: true);
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

    private static CosmosAvailabilityRequestStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosAvailabilityRequestStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Point-reads the single record for a request id by its id into a pooled response (no GC array) — a single-partition
    // point read, NotFound by status code (not an exception). Returns null on NotFound; otherwise the caller slices the
    // embedded doc off the LIVE response (CosmosJson.GetRawValue) and copies/parses it before this response is returned
    // to the pool — disposing the returned RentedResponse releases the buffer.
    private async ValueTask<CosmosJson.RentedResponse?> ReadResponseAsync(string id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    // Yields each matched request's embedded doc raw UTF-8 bytes (a slice into the pooled response page) — no base64
    // decode. The consumer copies it (ToPooledDocument into the list) within the iteration step, before the page rolls.
    // The query is cross-partition (List spans all requests); the SDK orders across partitions on the server.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);
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

    // The envelope's top-level fields: the request id (item id + partition key), the mirrored filterable fields List
    // queries and orders on, and the request document embedded verbatim.
    private readonly record struct EnvelopeFields(
        string Id,
        string Environment,
        string CreatedBy,
        string Status,
        string CreatedAt,
        byte[] Doc);
}