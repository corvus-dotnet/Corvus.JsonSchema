// <copyright file="CosmosDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture is a
/// single document keyed by (and partitioned on) its run id, carrying the audited <see cref="DraftRun"/> record
/// verbatim as a nested <c>record</c> object (embedded, not base64 — the runner registry's nested-<c>doc</c> idiom)
/// and the packed document + sources as a base64 <c>package</c> string (Cosmos stores JSON, so binary is base64 —
/// the catalog store's package idiom). Documents are written and read through the Cosmos <em>stream</em> APIs so
/// persistence flows through Corvus.Text.Json and never the SDK's reflection serializer.
/// </summary>
/// <remarks>
/// Provision the database and container once with <see cref="PrepareAsync(string, string, CancellationToken)"/>,
/// then open the store with <see cref="ConnectAsync(string, string, CancellationToken)"/>; the overloads taking a
/// <see cref="CosmosClient"/> let callers configure the client (for example a least-privileged data-plane managed
/// identity) themselves.
/// </remarks>
public sealed class CosmosDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string CapturesContainerId = "draft_runs";

    private readonly CosmosClient client;
    private readonly Container captures;
    private readonly bool ownsClient;

    private CosmosDraftRunStore(CosmosClient client, Container captures, bool ownsClient)
    {
        this.client = client;
        this.captures = captures;
        this.ownsClient = ownsClient;
    }

    /// <summary>Provisions the store's database and container over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string (typically the account key, which has management-plane rights).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's database and container over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Creating a database/container is a Cosmos <em>management-plane</em> operation — the data-plane RBAC roles
    /// (for example <c>Cosmos DB Built-in Data Contributor</c>) cannot do it. So provisioning needs the account key
    /// or a control-plane role and must be separated from the least-privileged data-plane credential used to
    /// <see cref="ConnectAsync(CosmosClient, string, CancellationToken)"/> the store for operation. Run this once at
    /// deploy/migration time.
    /// </remarks>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the store for operation against an already-provisioned database and container.</summary>
    /// <remarks>
    /// This creates no database or container, so it is safe to use a least-privileged data-plane credential. Call
    /// <see cref="PrepareAsync(string, string, CancellationToken)"/> once beforehand to provision.
    /// </remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosDraftRunStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosDraftRunStore>(Connect(client, databaseName, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c> holding
    /// only a data-plane role — so the store runs under a least-privileged principal with no account key. This
    /// creates no database or container; call <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosDraftRunStore> ConnectAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosDraftRunStore>(Connect(client, databaseName, ownsClient: false));
    }

    /// <summary>The Cosmos client options the store relies on.</summary>
    /// <remarks>
    /// The store reads and writes through the Cosmos stream APIs and serializes documents with Corvus.Text.Json, so
    /// no SDK serializer is configured.
    /// </remarks>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        string runId = id.Value;

        // Serialize the record once into a pooled buffer, then stream the envelope: the id, the record embedded
        // verbatim as a nested object (WriteRawValue — no spurious re-encode, the runner registry's nested-doc
        // idiom) and the (potentially large, ~KB) package base64-written straight into the writer from its memory
        // (the catalog store's package idiom). No owned byte[] for the record, no package.ToArray().
        using CosmosJson.RentedJson recordJson = CosmosJson.RentJson(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        using Stream stream = CosmosJson.WriteToStream(
            (RunId: runId, Record: recordJson, Package: package),
            static (Utf8JsonWriter writer, in (string RunId, CosmosJson.RentedJson Record, ReadOnlyMemory<byte> Package) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString("id"u8, c.RunId);
                writer.WritePropertyName("record"u8);
                writer.WriteRawValue(c.Record.Span, skipInputValidation: true);
                writer.WriteBase64String("package"u8, c.Package.Span);
                writer.WriteEndObject();
            });

        using ResponseMessage response = await this.captures.UpsertItemStreamAsync(stream, new PartitionKey(runId), cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.captures.ReadItemStreamAsync(id.Value, new PartitionKey(id.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);

        // The record was embedded verbatim, so its raw nested bytes parse straight into a pooled document (which
        // clones, so it survives the response buffer being returned).
        ReadOnlyMemory<byte> record = CosmosJson.GetRawValue(payload.Memory, "record"u8);
        return record.IsEmpty ? null : PersistedJson.ToPooledDocument<DraftRun>(record.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.captures.ReadItemStreamAsync(id.Value, new PartitionKey(id.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);

        // The package is a base64 JSON string; take its raw token (quotes included; base64 has no escapes), strip
        // the enclosing quotes to the base64 UTF-8, and decode it straight to bytes with no intermediate managed
        // base64 string — mirroring the catalog store's CatalogDocument.PackageBytes.
        ReadOnlyMemory<byte> raw = CosmosJson.GetRawValue(payload.Memory, "package"u8);
        return raw.IsEmpty ? null : CosmosJson.DecodeBase64Utf8(raw.Span[1..^1]);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.captures.DeleteItemStreamAsync(id.Value, new PartitionKey(id.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
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

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(CapturesContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosDraftRunStore Connect(CosmosClient client, string databaseName, bool ownsClient)
    {
        // GetDatabase/GetContainer return proxies without network I/O (no creation), so this is a pure data-plane
        // open against the already-provisioned resources.
        Database database = client.GetDatabase(databaseName);
        Container captures = database.GetContainer(CapturesContainerId);
        return new CosmosDraftRunStore(client, captures, ownsClient);
    }
}