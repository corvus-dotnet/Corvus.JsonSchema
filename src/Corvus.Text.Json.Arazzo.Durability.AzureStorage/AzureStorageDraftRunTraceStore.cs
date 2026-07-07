// <copyright file="AzureStorageDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug
/// (<c>$draft</c>) run's latest assembled metadata trace. Each run's trace is a block blob keyed by run id — exactly
/// as <see cref="AzureStorageDraftRunStore"/> holds its (potentially large) package as a blob (the trace, like the
/// package, can exceed Table storage's ~64KB per-property limit, so it is blob-only). Works against Azure Storage
/// and the Azurite emulator.
/// </summary>
/// <remarks>
/// Provision the container once with <see cref="PrepareAsync(string, CancellationToken)"/>, then open the store with
/// <see cref="ConnectAsync(string, CancellationToken)"/>. The put uploads the trace straight from its memory:
/// <see cref="BinaryData.FromBytes(ReadOnlyMemory{byte})"/> wraps the <see cref="ReadOnlyMemory{Byte}"/> without a
/// GC copy of the trace, matching <see cref="AzureStorageDraftRunStore"/>'s package upload.
/// </remarks>
public sealed class AzureStorageDraftRunTraceStore : IDraftRunTraceStore
{
    private const string DraftRunTraceContainer = "arazzo-draftruntraces";

    // The Blob SDK defaults to the newest REST API version, which the Azurite emulator (and older real
    // accounts) may not yet recognise. Pin to a broadly-supported version so requests are accepted everywhere.
    private const BlobClientOptions.ServiceVersion BlobApiVersion = BlobClientOptions.ServiceVersion.V2024_11_04;

    private readonly BlobContainerClient traces;

    private AzureStorageDraftRunTraceStore(BlobContainerClient traces)
    {
        this.traces = traces;
    }

    /// <summary>Provisions the store's blob container over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(BlobServiceClient, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create the container.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container exists (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)), cancellationToken);
    }

    /// <summary>
    /// Provisions the store's blob container. Container creation is a broader right than the per-blob data access
    /// the store needs at runtime, so run this once at deploy/migration time, separately from the least-privileged
    /// credential used to <see cref="ConnectAsync(BlobServiceClient, CancellationToken)"/> the store for operation.
    /// </summary>
    /// <param name="blobService">A blob service client (for example one built with a managed identity / <c>TokenCredential</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(BlobServiceClient blobService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        await blobService.GetBlobContainerClient(DraftRunTraceContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned container.</summary>
    /// <remarks>
    /// This creates no container, so it is safe to use a least-privileged data-plane credential. Call
    /// <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand to provision the resource.
    /// </remarks>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageDraftRunTraceStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)), cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c> holding
    /// only a data-plane role — so the store runs under a least-privileged principal with no key in a connection
    /// string. This creates no container; call <see cref="PrepareAsync(BlobServiceClient, CancellationToken)"/> once
    /// beforehand.
    /// </remarks>
    /// <param name="blobService">A blob service client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageDraftRunTraceStore> ConnectAsync(BlobServiceClient blobService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageDraftRunTraceStore>(new AzureStorageDraftRunTraceStore(blobService.GetBlobContainerClient(DraftRunTraceContainer)));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        // Upload the trace straight from its memory: BinaryData.FromBytes wraps the ReadOnlyMemory without a GC copy
        // of the trace (the package-blob idiom AzureStorageDraftRunStore uses). overwrite:true replaces the prior
        // trace, so a re-put overwrites.
        await this.traces.GetBlobClient(id.Value)
            .UploadAsync(BinaryData.FromBytes(traceUtf8), overwrite: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        BlobClient blob = this.traces.GetBlobClient(id.Value);
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
        // DeleteIfExistsAsync reports whether a blob existed and was deleted.
        Response<bool> response = await this.traces.GetBlobClient(id.Value)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Value;
    }
}