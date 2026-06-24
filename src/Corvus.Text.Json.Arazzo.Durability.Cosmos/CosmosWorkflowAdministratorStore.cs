// <copyright file="CosmosWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for
/// a base workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is one document in a small <c>workflow_administrators</c> container, partitioned by — and
/// identified within that partition by — a deterministic, opaque hash of the base workflow id, so a base id is a single
/// logical record reached by a single-partition point read. The record holds its <see cref="WorkflowAdministrators"/>
/// document verbatim as a raw nested JSON value (no base64 round-trip); the store-owned etag travels inside it. Documents
/// are written and read through the Cosmos <em>stream</em> APIs (no SDK serializer), so persistence flows through
/// Corvus.Text.Json. The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// The <see cref="PutAsync"/> create-or-replace reads the current document and compares its embedded etag before writing,
/// mirroring the other backends' optimistic concurrency: a create (<see cref="WorkflowEtag.None"/>) collides on the
/// deterministic item id and surfaces as a <see cref="HttpStatusCode.Conflict"/>, and a replace targets the existing item
/// by that same id. Authorization is the caller's concern, not this store's — it is a CAS key/value persistence seam.
/// </remarks>
public sealed class CosmosWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_administrators";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosWorkflowAdministratorStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosWorkflowAdministratorStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosWorkflowAdministratorStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkflowAdministratorStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosWorkflowAdministratorStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        using CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        if (payload is not { } page)
        {
            return null;
        }

        // The embedded doc is raw nested JSON (a slice of the live pooled response). Copy it into an owned pooled
        // document (no GC array) before the response buffer is returned at the end of this using.
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
        return doc.IsEmpty ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>(doc.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        string id = ItemId(baseWorkflowId);
        ParsedJsonDocument<WorkflowAdministrators>? updateDocument = null;
        Stream? updateStream = null;
        using (CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false))
        {
            if (payload is { } page && CosmosJson.GetRawValue(page.Memory, DocProperty) is { IsEmpty: false } doc)
            {
                // Parse the existing document NON-COPYING over the live response (no GC array, no pooled copy) to check the
                // etag and carry its immutable creation audit forward; both the parse and its consumers (SerializeUpdated +
                // EnvelopeStream, all synchronous) complete before the response buffer is returned at the end of this using.
                // SerializeUpdated produces an owned byte[] and EnvelopeStream a pooled stream, so neither outlives the page.
                using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(doc);

                // A record exists: the caller must hold its current etag (None means "I expected no record").
                if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                byte[] updated = WorkflowAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
                updateStream = EnvelopeStream(id, updated, out ParsedJsonDocument<WorkflowAdministrators> document);
                updateDocument = document;
            }
        }

        if (updateStream is not null && updateDocument is { } replaceDocument)
        {
            using (updateStream)
            {
                try
                {
                    using ResponseMessage response = await this.container.ReplaceItemStreamAsync(updateStream, id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return replaceDocument;
                }
                catch
                {
                    replaceDocument.Dispose();
                    throw;
                }
            }
        }

        // No record yet: materialization is only valid against the None etag (the v1-derived default).
        if (!expectedEtag.IsNone)
        {
            throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
        }

        byte[] created = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
        using (Stream createStream = EnvelopeStream(id, created, out ParsedJsonDocument<WorkflowAdministrators> createDocument))
        {
            try
            {
                using ResponseMessage response = await this.container.CreateItemStreamAsync(createStream, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // A concurrent create won the race between the read above and this write — the caller's None etag no
                    // longer reflects reality (a record now exists), so surface it as the same conflict the read path would.
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                response.EnsureSuccessStatusCode();
                return createDocument;
            }
            catch
            {
                createDocument.Dispose();
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

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // A deterministic, opaque, Cosmos-id-safe document id (and partition key) from the base workflow id, which may
    // contain characters unsuitable for an item id. A single base id maps to one item in its own partition, so a create
    // collides on the id (a 409) and a replace/read is a single-partition point operation.
    private static string ItemId(string baseWorkflowId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(baseWorkflowId), hash);
        return "wadmin-" + Convert.ToHexStringLower(hash);
    }

    // Serializes the {id, pk, doc} envelope into a pooled stream and builds the caller's pooled return document from the
    // same administration-record bytes. The record document is itself JSON, so embed it verbatim as a nested value — no
    // base64 wrap (which would be a spurious encode here + decode on read). It is valid JSON we produced, so skip
    // validation. On any failure building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(string id, byte[] doc, out ParsedJsonDocument<WorkflowAdministrators> document)
    {
        document = PersistedJson.ToPooledDocument<WorkflowAdministrators>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);
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

    private static CosmosWorkflowAdministratorStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosWorkflowAdministratorStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Point-reads the single record for a base workflow id by its deterministic id into a pooled response (no GC array) —
    // a single-partition point read, NotFound by status code (not an exception). Returns null on NotFound; otherwise the
    // caller slices the embedded doc off the LIVE response (CosmosJson.GetRawValue, raw nested JSON — no base64) and
    // copies/parses it before this response is returned to the pool — disposing the returned RentedResponse releases it.
    private async ValueTask<CosmosJson.RentedResponse?> ReadResponseAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        string id = ItemId(baseWorkflowId);
        using ResponseMessage response = await this.container.ReadItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }
}