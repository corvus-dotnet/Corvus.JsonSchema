// <copyright file="CosmosNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055). Each job is one document
/// in a small <c>native_build_jobs</c> container, partitioned by — and identified within that partition by — its
/// target-derived id, so a job is a single logical record reached by a single-partition point read. The record holds its
/// <see cref="NativeBuildJob"/> schema document verbatim as a raw nested JSON value; the filterable fields (status, target
/// tuple, creation instant) are mirrored to top-level envelope properties so <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/>
/// can query and order on them, and the store-owned etag travels inside the embedded document. Documents flow through the
/// Cosmos <em>stream</em> APIs (no SDK serializer), so persistence flows through Corvus.Text.Json. The id is derived from the
/// target tuple, so <see cref="EnqueueAsync"/> is an idempotent upsert. Mirrors <see cref="CosmosAvailabilityRequestStore"/>,
/// keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>The claim queries the oldest Queued job, then transitions it to Building by replacing it under an
/// <c>If-Match</c> on the native <c>_etag</c>; a concurrent claim that already advanced the record fails the etag check (412)
/// and the claim retries the next-oldest, so two workers never claim the same job.</remarks>
public sealed class CosmosNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private const string ContainerId = "native_build_jobs";

    // The bounded number of oldest-Queued candidates a single claim will race for before giving up; each lost race means a
    // concurrent worker won that candidate, so under real contention a claim succeeds well within this budget.
    private const int MaxClaimAttempts = 64;

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosNativeBuildJobStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosNativeBuildJobStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosNativeBuildJobStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosNativeBuildJobStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosNativeBuildJobStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = NativeBuildJobSerialization.SerializeNew(id, draft, now, NewEtag());
        var fields = new EnvelopeFields(
            id,
            draft.BaseWorkflowIdValue,
            draft.VersionNumberValue,
            draft.EnvironmentValue,
            draft.RuntimeIdentifierValue,
            NativeBuildJobStatusNames.Queued,
            now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            json);
        using Stream stream = EnvelopeStream(in fields, out ParsedJsonDocument<NativeBuildJob> document);
        try
        {
            // Idempotent per target: the id is derived from the tuple, so a repeated enqueue upserts the same item — it
            // resets the job to Queued (SerializeNew omits startedAt/completedAt/failureReason/claimedBy).
            using ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
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
        return doc.IsEmpty ? null : PersistedJson.ToPooledDocument<NativeBuildJob>(doc.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        // The oldest Queued candidates in (createdAt, id) order (bounded). Each embedded doc carries the id, so the candidate
        // ids are recovered from the projection; the authoritative native _etag is then taken from a point read for the CAS.
        var definition = new QueryDefinition("SELECT c.doc FROM c WHERE c.status = @queued ORDER BY c.createdAt, c.id OFFSET 0 LIMIT @lim")
            .WithParameter("@queued", NativeBuildJobStatusNames.Queued)
            .WithParameter("@lim", MaxClaimAttempts);
        var candidateIds = new List<string>();
        await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
        {
            candidateIds.Add(NativeBuildJob.FromJson(doc).IdValue);
        }

        foreach (string id in candidateIds)
        {
            using ResponseMessage read = await this.container.ReadItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (read.StatusCode == HttpStatusCode.NotFound)
            {
                continue; // gone since the projection read
            }

            read.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> currentDoc = CosmosJson.GetRawValue(payload.Memory, DocProperty);
            if (currentDoc.IsEmpty)
            {
                continue;
            }

            ParsedJsonDocument<NativeBuildJob> claimedDocument;
            Stream stream;
            using (ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(currentDoc))
            {
                if (!current.RootElement.HasStatus(NativeBuildJobStatus.Queued))
                {
                    continue; // no longer queued (concurrently claimed) — try the next
                }

                byte[] claimed = NativeBuildJobSerialization.SerializeClaimed(current.RootElement, claimedBy, this.timeProvider.GetUtcNow(), NewEtag());
                var fields = new EnvelopeFields(
                    id,
                    current.RootElement.BaseWorkflowIdValue,
                    current.RootElement.VersionNumberValue,
                    current.RootElement.EnvironmentValue,
                    current.RootElement.RuntimeIdentifierValue,
                    NativeBuildJobStatusNames.Building,
                    current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                    claimed);
                stream = EnvelopeStream(in fields, out claimedDocument);
            }

            using (stream)
            {
                var options = new ItemRequestOptions { IfMatchEtag = read.Headers.ETag };
                using ResponseMessage replaced = await this.container.ReplaceItemStreamAsync(stream, id, new PartitionKey(id), options, cancellationToken).ConfigureAwait(false);
                if (replaced.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
                {
                    claimedDocument.Dispose();
                    continue; // a concurrent claim advanced the record — retry the next-oldest
                }

                try
                {
                    replaced.EnsureSuccessStatusCode();
                    return claimedDocument;
                }
                catch
                {
                    claimedDocument.Dispose();
                    throw;
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ParsedJsonDocument<NativeBuildJob> document;
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

            // Parse the existing document NON-COPYING over the live response (no GC array, no pooled copy) to enforce the
            // Building state, check the etag and carry its immutable target fields forward; a stale etag throws
            // NativeBuildJobConflictException. The parse and its synchronous consumers (state check, SerializeCompletion, the
            // mirror reads, EnvelopeStream) all complete before the response buffer is returned at the end of this using.
            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc);
            if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
            {
                throw new NativeBuildJobStateException(id, $"The native build job '{id}' cannot be completed because it is not building.");
            }

            byte[] json = NativeBuildJobSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), NewEtag());
            var fields = new EnvelopeFields(
                id,
                current.RootElement.BaseWorkflowIdValue,
                current.RootElement.VersionNumberValue,
                current.RootElement.EnvironmentValue,
                current.RootElement.RuntimeIdentifierValue,
                NativeBuildJobStatusNames.ToWire(completion.Status),
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
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        // A cross-partition query over the mirrored top-level filterable fields, oldest-first by creation instant then id
        // (the id tiebreak makes the order total). The embedded doc is the only projection — each job's etag and lifecycle
        // fields live inside it. Every criterion binds as a parameter (no concatenation).
        var conditions = new List<string>(5);
        AppendConditions(conditions, query);

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        QueryDefinition definition = WithParameters(new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.createdAt, c.id"), query);

        var list = new PooledDocumentList<NativeBuildJob>();
        try
        {
            await foreach (ReadOnlyMemory<byte> doc in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
            {
                list.Add(PersistedJson.ToPooledDocument<NativeBuildJob>(doc.Span));
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
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A single-partition point read on the derived id (the target tuple maps to a unique id) — never a scan.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using CosmosJson.RentedResponse? payload = await this.ReadResponseAsync(id, cancellationToken).ConfigureAwait(false);
        if (payload is not { } page)
        {
            return false;
        }

        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
        if (doc.IsEmpty)
        {
            return false;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc);
        return current.RootElement.HasStatus(NativeBuildJobStatus.Ready);
    }

    /// <inheritdoc/>
    // This project generates its own Cosmos-namespace JsonString (per-root type identity), so the seam parameter is fully
    // qualified to the core JsonString INativeBuildJobStore's signature uses.
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, global::Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Cosmos parameters need (the leaf) only here —
        // createdAt as the ISO-8601 "o" form the mirrored c.createdAt stores (reconstructed from the token's UTC ticks).
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(NativeBuildJobContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (NativeBuildJobContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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

        var conditions = new List<string>(6);
        AppendConditions(conditions, query);

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): Cosmos orders strings ordinally, so the ISO createdAt order is
            // chronological and the id order is byte-ordinal — the same total order the in-memory pager uses.
            conditions.Add("(c.createdAt > @ca OR (c.createdAt = @ca AND c.id > @id))");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        QueryDefinition definition = WithParameters(new QueryDefinition("SELECT c.doc FROM c" + where + " ORDER BY c.createdAt, c.id"), query);
        if (cursorCreatedAt is not null)
        {
            definition = definition.WithParameter("@ca", cursorCreatedAt).WithParameter("@id", cursorId);
        }

        var page = new PooledDocumentList<NativeBuildJob>(pageSize);
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

                page.Add(PersistedJson.ToPooledDocument<NativeBuildJob>(doc.Span));
            }

            if (!hasMore)
            {
                return NativeBuildJobPage.Create(page);
            }

            NativeBuildJob last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return NativeBuildJobPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        // Bounded count over an id-only projection capped at cap + 1 (OFFSET 0 LIMIT), counting the returned ids
        // client-side. Cosmos has NO bounded server-side COUNT: a bare COUNT ignores any outer LIMIT and scans the whole
        // matching set, and wrapping COUNT around a cap-limited subquery is rejected. So the outer LIMIT + client-side count
        // is the only bounded option; do not use COUNT.
        var conditions = new List<string>(5);
        AppendConditions(conditions, query);

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        QueryDefinition definition = WithParameters(new QueryDefinition("SELECT c.id FROM c" + where + " ORDER BY c.createdAt, c.id OFFSET 0 LIMIT @lim"), query)
            .WithParameter("@lim", cap + 1);

        int total = 0;
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(definition);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                _ = element; // id-only projection — only its presence counts toward the bounded total
                total++;
            }
        }

        return total > cap ? (cap, true) : (total, false);
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

    // Appends the shared list conditions (status / target tuple) as parameter references; a null criterion adds nothing.
    private static void AppendConditions(List<string> conditions, NativeBuildJobQuery query)
    {
        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.BaseWorkflowId is not null)
        {
            conditions.Add("c.baseWorkflowId = @base");
        }

        if (query.VersionNumber is not null)
        {
            conditions.Add("c.versionNumber = @ver");
        }

        if (query.Environment is not null)
        {
            conditions.Add("c.environment = @env");
        }

        if (query.RuntimeIdentifier is not null)
        {
            conditions.Add("c.runtimeIdentifier = @rid");
        }
    }

    // Binds the parameters for the conditions AppendConditions produced.
    private static QueryDefinition WithParameters(QueryDefinition definition, NativeBuildJobQuery query)
    {
        if (query.Status is { } status)
        {
            definition = definition.WithParameter("@status", NativeBuildJobStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            definition = definition.WithParameter("@base", baseWorkflowId);
        }

        if (query.VersionNumber is { } versionNumber)
        {
            definition = definition.WithParameter("@ver", versionNumber);
        }

        if (query.Environment is { } environment)
        {
            definition = definition.WithParameter("@env", environment);
        }

        if (query.RuntimeIdentifier is { } runtimeIdentifier)
        {
            definition = definition.WithParameter("@rid", runtimeIdentifier);
        }

        return definition;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Serializes the {id, pk, baseWorkflowId, versionNumber, environment, runtimeIdentifier, status, createdAt, doc} envelope
    // into a pooled stream and builds the caller's pooled return document from the same job bytes. The job document is itself
    // JSON, so embed it verbatim as a nested value — no base64 wrap. It is valid JSON we produced, so skip validation. On any
    // failure building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(in EnvelopeFields fields, out ParsedJsonDocument<NativeBuildJob> document)
    {
        document = PersistedJson.ToPooledDocument<NativeBuildJob>(fields.Doc);
        try
        {
            return CosmosJson.WriteToStream(
                in fields,
                static (Utf8JsonWriter writer, in EnvelopeFields c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);
                    writer.WriteString("baseWorkflowId"u8, c.BaseWorkflowId);
                    writer.WriteNumber("versionNumber"u8, c.VersionNumber);
                    writer.WriteString("environment"u8, c.Environment);
                    writer.WriteString("runtimeIdentifier"u8, c.RuntimeIdentifier);
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

    private static CosmosNativeBuildJobStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosNativeBuildJobStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Point-reads the single record for a job id into a pooled response (no GC array) — a single-partition point read,
    // NotFound by status code (not an exception). Returns null on NotFound; otherwise the caller slices the embedded doc off
    // the LIVE response (CosmosJson.GetRawValue) and copies/parses it before this response is returned to the pool.
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

    // Yields each matched job's embedded doc raw UTF-8 bytes (a slice into the pooled response page) — no base64 decode. The
    // consumer copies it (ToPooledDocument into the list, or parses the id) within the iteration step, before the page rolls.
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

    // The envelope's top-level fields: the job id (item id + partition key), the mirrored filterable fields List queries and
    // orders on, and the job document embedded verbatim.
    private readonly record struct EnvelopeFields(
        string Id,
        string BaseWorkflowId,
        int VersionNumber,
        string Environment,
        string RuntimeIdentifier,
        string Status,
        string CreatedAt,
        byte[] Doc);
}