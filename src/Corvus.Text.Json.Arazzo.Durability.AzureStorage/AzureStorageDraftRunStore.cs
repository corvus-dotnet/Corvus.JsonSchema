// <copyright file="AzureStorageDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. The audited
/// <see cref="DraftRun"/> record is held verbatim as its JSON document in a <c>Record</c> property of a Table
/// entity keyed by run id (the runner registry's <c>Doc</c> idiom), while the packed document + sources — which
/// can exceed Table storage's ~64KB per-property limit — is a block blob keyed by run id, exactly as the catalog
/// store holds its canonical package in a blob and its metadata in a table. Works against Azure Storage and the
/// Azurite emulator.
/// </summary>
/// <remarks>
/// Provision the container and table once with <see cref="PrepareAsync(string, CancellationToken)"/>, then open
/// the store with <see cref="ConnectAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class AzureStorageDraftRunStore : IDraftRunStore
{
    private const string DraftRunContainer = "arazzo-draftruns";
    private const string DraftRunTable = "arazzodraftruns";
    private const string PartitionKey = "draftrun";

    // The Blob SDK defaults to the newest REST API version, which the Azurite emulator (and older real
    // accounts) may not yet recognise. Pin to a broadly-supported version so requests are accepted everywhere.
    private const BlobClientOptions.ServiceVersion BlobApiVersion = BlobClientOptions.ServiceVersion.V2024_11_04;

    private readonly BlobContainerClient packages;
    private readonly TableClient captures;

    private AzureStorageDraftRunStore(BlobContainerClient packages, TableClient captures)
    {
        this.packages = packages;
        this.captures = captures;
    }

    /// <summary>Provisions the store's blob container and table over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create the container and table.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            cancellationToken);
    }

    /// <summary>
    /// Provisions the store's blob container and table. Container/table creation is a broader right than the
    /// per-blob / per-entity data access the store needs at runtime, so run this once at deploy/migration time,
    /// separately from the least-privileged credential used to <see cref="ConnectAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/>
    /// the store for operation.
    /// </summary>
    /// <param name="blobService">A blob service client (for example one built with a managed identity / <c>TokenCredential</c>).</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(BlobServiceClient blobService, TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);

        await blobService.GetBlobContainerClient(DraftRunContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(DraftRunTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned container and table.</summary>
    /// <remarks>
    /// This creates no container or table, so it is safe to use a least-privileged data-plane credential. Call
    /// <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand to provision the resources.
    /// </remarks>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageDraftRunStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            cancellationToken);
    }

    /// <summary>Opens the store for operation over caller-supplied service clients.</summary>
    /// <remarks>
    /// Supply clients the caller configured — for example with a managed identity / <c>TokenCredential</c>
    /// holding only data-plane roles — so the store runs under a least-privileged principal with no key in a
    /// connection string. This creates no container or table; call <see cref="PrepareAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="blobService">A blob service client.</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageDraftRunStore> ConnectAsync(BlobServiceClient blobService, TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();

        BlobContainerClient packages = blobService.GetBlobContainerClient(DraftRunContainer);
        TableClient captures = tableService.GetTableClient(DraftRunTable);
        return new ValueTask<AzureStorageDraftRunStore>(new AzureStorageDraftRunStore(packages, captures));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));

        // Upload the (potentially large, ~KB) package straight from its memory: BinaryData.FromBytes wraps the
        // ReadOnlyMemory without a GC copy of the whole package (the catalog store's package-blob idiom). The
        // record is the small audited document, held verbatim in the Table entity (the runner registry's idiom).
        await this.packages.GetBlobClient(id.Value)
            .UploadAsync(BinaryData.FromBytes(package), overwrite: true, cancellationToken)
            .ConfigureAwait(false);

        var entity = new TableEntity(PartitionKey, id.Value)
        {
            ["Record"] = recordUtf8,
        };
        await this.captures.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> existing = await this.captures
            .GetEntityIfExistsAsync<TableEntity>(PartitionKey, id.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return null;
        }

        byte[]? record = existing.Value!.GetBinary("Record");
        return record is null ? null : PersistedJson.ToPooledDocument<DraftRun>(record);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        BlobClient blob = this.packages.GetBlobClient(id.Value);
        try
        {
            Response<BlobDownloadResult> response = await blob.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        Response response = await this.captures
            .DeleteEntityAsync(PartitionKey, id.Value, ETag.All, cancellationToken)
            .ConfigureAwait(false);
        await this.packages.GetBlobClient(id.Value).DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        // DeleteEntityAsync returns 404 (not an exception) when no entity existed.
        return response.Status != 404;
    }
}