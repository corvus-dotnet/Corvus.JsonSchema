// <copyright file="CosmosEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a
/// deployment environment (design §5.5): its decision state and audit metadata. Each authorization is one document in a
/// small <c>environment_runner_authorizations</c> container whose id is a deterministic, opaque hash of its composite key
/// (<c>environment</c>, <c>runnerId</c>) and which is its own partition key, so ensure-pending / get / decide are
/// single-partition point operations and a duplicate ensure-pending collides on the id (surfacing as a
/// <see cref="HttpStatusCode.Conflict"/> the idempotent create resolves by re-reading). The record holds its
/// <see cref="EnvironmentRunnerAuthorization"/> schema document verbatim as a raw nested JSON value (no base64 round-trip);
/// the filterable fields (status, environment, runner id) are mirrored to top-level envelope properties so
/// <see cref="ListAsync(RunnerAuthorizationQuery, CancellationToken)"/> can query and order on them, and the store-owned
/// etag travels inside the embedded document. Documents are written and read through the Cosmos <em>stream</em> APIs (no SDK
/// serializer), so persistence flows through Corvus.Text.Json. Mirrors <see cref="CosmosAvailabilityRequestStore"/>, keyed by
/// environment + runner rather than a single id.
/// </summary>
/// <remarks>
/// Read/return methods hand back pooled documents whose lifetime the caller owns. <see cref="DecideAsync"/> reads the
/// current document, checks its embedded etag (through <see cref="EnvironmentRunnerAuthorizationSerialization.SerializeDecision"/>)
/// and replaces it by its id — mirroring the other backends' optimistic concurrency, so two administrators cannot
/// double-decide the same authorization.
/// </remarks>
public sealed class CosmosEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private const string ContainerId = "environment_runner_authorizations";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosEnvironmentRunnerAuthorizationStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosEnvironmentRunnerAuthorizationStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosEnvironmentRunnerAuthorizationStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosEnvironmentRunnerAuthorizationStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        string itemId = ItemId(environment, runnerId);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending).
        byte[]? existing = await this.ReadDocumentAsync(itemId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing);
        }

        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), NewEtag());

        bool conflict;
        var fields = new EnvelopeFields(itemId, environment, runnerId, RunnerAuthorizationStatusNames.Pending, json);
        using (Stream stream = EnvelopeStream(in fields, out ParsedJsonDocument<EnvironmentRunnerAuthorization> document))
        {
            try
            {
                using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
                conflict = response.StatusCode == HttpStatusCode.Conflict;
                if (!conflict)
                {
                    response.EnsureSuccessStatusCode();
                    return document;
                }
            }
            catch
            {
                document.Dispose();
                throw;
            }

            // A concurrent ensure-pending already created the record; the built document is unused, so dispose it (exactly
            // once — outside the catch) and re-read the winner below.
            document.Dispose();
        }

        // The create lost a race; re-read and return the existing record unchanged (idempotent).
        byte[]? raced = conflict ? await this.ReadDocumentAsync(itemId, cancellationToken).ConfigureAwait(false) : null;
        return raced is not null
            ? PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(raced)
            : throw new InvalidOperationException("The runner authorization create conflicted but could not be re-read.");
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        byte[]? json = await this.ReadDocumentAsync(ItemId(environment, runnerId), cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        // A cross-partition query over the mirrored top-level filterable fields, ordered by (environment, runnerId) — the
        // total order the keyset pager uses (both are byte-ordinal under Cosmos's string ordering). The embedded doc is the
        // only projection — each authorization's etag and audit fields live inside it. Every criterion binds as a parameter
        // (no concatenation).
        var conditions = new List<string>(3);
        string[]? adminNames = AppendConditions(conditions, query);

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.env, c.runnerId");
        definition = WithParameters(definition, query, adminNames);

        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        try
        {
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, int.MaxValue, cancellationToken).ConfigureAwait(false))
            {
                list.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc.Span));
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
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the Cosmos parameters need (the leaf) only
        // here. Undefined token = first page; a malformed token throws FormatException.
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

        var conditions = new List<string>(4);
        string[]? adminNames = AppendConditions(conditions, query);

        if (cursorEnvironment is not null)
        {
            // Keyset seek strictly past (environment, runnerId): Cosmos orders strings ordinally, so the (env, runnerId)
            // order is byte-ordinal — the same total order the in-memory pager uses.
            conditions.Add("(c.env > @ce OR (c.env = @ce AND c.runnerId > @cr))");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.env, c.runnerId");
        definition = WithParameters(definition, query, adminNames);
        if (cursorEnvironment is not null)
        {
            definition = definition.WithParameter("@ce", cursorEnvironment).WithParameter("@cr", cursorRunnerId);
        }

        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            bool hasMore = false;
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, pageSize + 1, cancellationToken).ConfigureAwait(false))
            {
                if (page.Count == pageSize)
                {
                    hasMore = true; // a row beyond the page exists → there is a next page; stop early
                    break;
                }

                page.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc.Span));
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

        string itemId = ItemId(environment, runnerId);
        ParsedJsonDocument<EnvironmentRunnerAuthorization> document;
        Stream stream;
        using (CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(itemId, cancellationToken).ConfigureAwait(false))
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
            // etag and carry its immutable fields forward; a stale etag throws RunnerAuthorizationConflictException. The
            // status mirror moves to the decided value; the environment/runner mirrors are immutable, so they carry through
            // from the stored record. The parse and its synchronous consumers (SerializeDecision, the mirror reads,
            // EnvelopeStream) all complete before the response buffer is returned at the end of this using.
            using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc);
            byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), NewEtag());
            var fields = new EnvelopeFields(
                itemId,
                current.RootElement.EnvironmentValue,
                current.RootElement.RunnerIdValue,
                RunnerAuthorizationStatusNames.ToWire(decision.Status),
                json);
            stream = EnvelopeStream(in fields, out document);
        }

        using (stream)
        {
            try
            {
                using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, itemId, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // A deterministic, opaque, Cosmos-id-safe document id from the composite key. The id is also the partition key, so
    // ensure-pending / get / decide are single-partition point operations and a duplicate (environment, runnerId)
    // ensure-pending collides on the id and surfaces as a 409 — mirroring the relational backends' composite-primary-key
    // uniqueness.
    private static string ItemId(string environment, string runnerId)
    {
        string discriminator = string.Create(CultureInfo.InvariantCulture, $"{environment} {runnerId}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(discriminator), hash);
        return "rauth-" + Convert.ToHexStringLower(hash);
    }

    // Appends the shared list filters (status / environment) and the approver-inbox filter (env IN the administered set) to
    // the conditions, returning the administered-set parameter names so the caller can bind them. The set is never empty
    // here (the handler short-circuits a caller who administers nothing); a null set (the non-inbox modes) adds nothing.
    private static string[]? AppendConditions(List<string> conditions, RunnerAuthorizationQuery query)
    {
        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.Environment is not null)
        {
            conditions.Add("c.env = @env");
        }

        if (query.AdministeredEnvironments is not { Count: > 0 } set)
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

    // Binds the filter parameters (status / environment / administered-environment set) onto the query definition, paired
    // with AppendConditions.
    private static QueryDefinition WithParameters(QueryDefinition definition, RunnerAuthorizationQuery query, string[]? adminNames)
    {
        if (query.Status is { } status)
        {
            definition = definition.WithParameter("@status", RunnerAuthorizationStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            definition = definition.WithParameter("@env", environment);
        }

        if (adminNames is not null && query.AdministeredEnvironments is { } administered)
        {
            for (int i = 0; i < adminNames.Length; i++)
            {
                definition = definition.WithParameter(adminNames[i], administered[i]);
            }
        }

        return definition;
    }

    // Serializes the {id, pk, env, runnerId, status, doc} envelope into a pooled stream and builds the caller's pooled
    // return document from the same source bytes. The authorization document is embedded verbatim (no SDK serializer); the
    // mirrored filterable fields are projected so List can ORDER BY and filter on them server-side. On any failure building
    // the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(in EnvelopeFields fields, out ParsedJsonDocument<EnvironmentRunnerAuthorization> document)
    {
        document = PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(fields.Doc);
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
                    writer.WriteString("runnerId"u8, c.RunnerId);
                    writer.WriteString("status"u8, c.Status);

                    // The authorization document is itself JSON, so embed it verbatim as a nested value — no base64 wrap
                    // (which would be a spurious encode here + decode on read). It is valid JSON we produced, so skip
                    // validation.
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

        // The default indexing policy indexes all paths, which serves the list axis: a composite (env, runnerId) ORDER BY
        // over the mirrored top-level fields.
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosEnvironmentRunnerAuthorizationStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosEnvironmentRunnerAuthorizationStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Point-reads the authorization document's embedded raw UTF-8 bytes (its envelope's `doc` value) for the id, copied out
    // of the response page (the bytes outlive the response), or null if the id is absent.
    private async ValueTask<byte[]?> ReadDocumentAsync(string itemId, CancellationToken cancellationToken)
    {
        using CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(itemId, cancellationToken).ConfigureAwait(false);
        if (payload is not { } page)
        {
            return null;
        }

        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
        return doc.IsEmpty ? null : doc.ToArray();
    }

    // Point-reads the single record for (environment, runnerId) by its derived id into a pooled response (no GC array) — a
    // single-partition point read, NotFound by status code (not an exception). Returns null on NotFound; otherwise the
    // caller slices the embedded doc off the LIVE response (CosmosJson.GetRawValue) and copies/parses it before this
    // response is returned to the pool — disposing the returned RentedResponse releases the buffer.
    private async ValueTask<CosmosJson.RentedResponse?> ReadResponseAsync(string itemId, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(itemId, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    // Yields each matched authorization's embedded doc raw UTF-8 bytes (a slice into the pooled response page) — no base64
    // decode. The consumer copies it (ToPooledDocument into the list) within the iteration step, before the page rolls. The
    // query is cross-partition (List spans all authorizations); the SDK orders across partitions on the server. One extra
    // row beyond the page is fetched as a look-ahead.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, int maxItemCount, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { MaxItemCount = maxItemCount };
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query, requestOptions: options);
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

    // The envelope's top-level fields: the derived item id (item id + partition key), the mirrored filterable fields List
    // queries and orders on, and the authorization document embedded verbatim.
    private readonly record struct EnvelopeFields(
        string Id,
        string Environment,
        string RunnerId,
        string Status,
        byte[] Doc);
}