// <copyright file="CosmosAccessRequestStore.cs" company="Endjin Limited">
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
/// An Azure Cosmos DB-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5): a principal's request
/// for elevated capability on a workflow, its decision state, and audit metadata. Each request is one document in a
/// small <c>workflow_access_requests</c> container, partitioned by — and identified within that partition by — its own
/// id, so a request is a single logical record reached by a single-partition point read. The record holds its
/// <see cref="AccessRequest"/> schema document verbatim as a raw nested JSON value (no base64 round-trip); the
/// filterable fields (status, target workflow, subject, creation instant) are mirrored to top-level envelope properties
/// so <see cref="ListAsync"/> can query and order on them, and the store-owned etag travels inside the embedded
/// document. Documents are written and read through the Cosmos <em>stream</em> APIs (no SDK serializer), so persistence
/// flows through Corvus.Text.Json.
/// </summary>
/// <remarks>
/// Read/return methods hand back pooled documents whose lifetime the caller owns. <see cref="DecideAsync"/> reads the
/// current document, checks its embedded etag (through <see cref="AccessRequestSerialization.SerializeDecision"/>) and
/// replaces it by its id — mirroring the other backends' optimistic concurrency, so two administrators cannot
/// double-decide the same request.
/// </remarks>
public sealed class CosmosAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_access_requests";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosAccessRequestStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosAccessRequestStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosAccessRequestStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosAccessRequestStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosAccessRequestStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, now, NewEtag());
        var fields = new EnvelopeFields(
            id,
            draft.BaseWorkflowIdValue,
            draft.SubjectClaimTypeValue,
            draft.SubjectClaimValueValue,
            AccessRequestStatusNames.Pending,
            now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            json);
        using MemoryStream stream = EnvelopeStream(in fields, out ParsedJsonDocument<AccessRequest> document);
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
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<AccessRequest>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        // A cross-partition query over the mirrored top-level filterable fields, oldest-first by creation instant then
        // id (the id tiebreak makes the order total). The embedded doc is the only projection — each request's etag and
        // audit fields live inside it. Every criterion binds as a parameter (no concatenation).
        var conditions = new List<string>(4);
        if (query.Status is { } status)
        {
            conditions.Add("c.status = @status");
        }

        if (query.BaseWorkflowId is not null)
        {
            conditions.Add("c.bw = @bw");
        }

        if (query.SubjectClaimType is not null)
        {
            conditions.Add("c.st = @st");
        }

        if (query.SubjectClaimValue is not null)
        {
            conditions.Add("c.sv = @sv");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.createdAt, c.id");
        if (query.Status is { } statusFilter)
        {
            definition = definition.WithParameter("@status", AccessRequestStatusNames.ToWire(statusFilter));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            definition = definition.WithParameter("@bw", baseWorkflowId);
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            definition = definition.WithParameter("@st", subjectType);
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            definition = definition.WithParameter("@sv", subjectValue);
        }

        var list = new PooledDocumentList<AccessRequest>();
        try
        {
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
            {
                list.Add(PersistedJson.ToPooledDocument<AccessRequest>(doc.Span));
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
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? existing = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // Parse the existing document once (pooled, inside SerializeDecision) to check the etag and carry its immutable
        // fields forward; a stale etag throws AccessRequestConflictException. The status mirror moves to the decided
        // value; the workflow/subject/creation mirrors are immutable, so they carry through from the stored record.
        byte[] json = AccessRequestSerialization.SerializeDecision(existing, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var fields = new EnvelopeFields(
            id,
            EnvelopeString(existing, "bw"u8),
            EnvelopeString(existing, "st"u8),
            EnvelopeString(existing, "sv"u8),
            AccessRequestStatusNames.ToWire(decision.Status),
            EnvelopeString(existing, "createdAt"u8),
            json);
        using MemoryStream stream = EnvelopeStream(in fields, out ParsedJsonDocument<AccessRequest> document);
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

    // Serializes the {id, pk, bw, st, sv, status, createdAt, doc} envelope into a pooled stream and builds the caller's
    // pooled return document from the same request bytes. The request document is itself JSON, so embed it verbatim as a
    // nested value — no base64 wrap (which would be a spurious encode here + decode on read). It is valid JSON we
    // produced, so skip validation. On any failure building the stream, the return document is disposed before the
    // exception escapes.
    private static MemoryStream EnvelopeStream(in EnvelopeFields fields, out ParsedJsonDocument<AccessRequest> document)
    {
        document = PersistedJson.ToPooledDocument<AccessRequest>(fields.Doc);
        try
        {
            return CosmosJson.WriteToStream(
                in fields,
                static (Utf8JsonWriter writer, in EnvelopeFields c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);
                    writer.WriteString("bw"u8, c.BaseWorkflowId);
                    writer.WriteString("st"u8, c.SubjectClaimType);
                    writer.WriteString("sv"u8, c.SubjectClaimValue);
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

    // Reads a mirrored top-level string from the stored ENVELOPE (not the embedded doc). DocumentAsync hands back only
    // the embedded doc, so DecideAsync re-reads the envelope's immutable mirrors (bw/st/sv/createdAt) here to re-stamp
    // them on the replacement. Always present on a record this store wrote.
    private static string EnvelopeString(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(envelope);
        if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool match = reader.ValueTextEquals(propertyUtf8);
                reader.Read();
                if (match)
                {
                    return reader.GetString() ?? string.Empty;
                }

                reader.Skip();
            }
        }

        return string.Empty;
    }

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosAccessRequestStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosAccessRequestStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Reads the single record for a request id by its id — a single-partition point read, NotFound by status code (not
    // an exception). The embedded doc is raw nested JSON (no base64); copy it out, as it outlives the pooled response
    // page (the caller parses it, or DecideAsync checks its etag and replaces from it).
    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(payload.Memory, DocProperty);
        return doc.IsEmpty ? null : doc.ToArray();
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
        string BaseWorkflowId,
        string SubjectClaimType,
        string SubjectClaimValue,
        string Status,
        string CreatedAt,
        byte[] Doc);
}