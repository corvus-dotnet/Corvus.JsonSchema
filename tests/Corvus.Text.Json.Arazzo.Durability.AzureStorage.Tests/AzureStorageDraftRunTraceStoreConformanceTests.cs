// <copyright file="AzureStorageDraftRunTraceStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared draft-run-trace-store conformance suite against <see cref="AzureStorageDraftRunTraceStore"/> over
/// the Azurite emulator in a container. Each test gets an empty store (the trace container's blobs are cleared).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageDraftRunTraceStoreConformanceTests : DraftRunTraceStoreConformance
{
    private static AzuriteContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        // Testcontainers.Azurite still defaults to a very old Azurite image; pin a recent one so it
        // recognises the pinned Blob REST API version. The store pins that version, so no command override
        // is needed.
        container = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
        await container.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override async ValueTask<IDraftRunTraceStore> CreateStoreAsync()
    {
        string connectionString = container.GetConnectionString();

        // Build the service client the caller owns (here from the Azurite connection string; in production this
        // would carry a managed identity / TokenCredential). The Blob version is pinned to one Azurite accepts. The
        // store is then provisioned and opened over this caller-supplied client.
        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));

        // Provision the container (the admin/DDL step), then reset data, then open for operation.
        await AzureStorageDraftRunTraceStore.PrepareAsync(blobService);

        BlobContainerClient traces = blobService.GetBlobContainerClient("arazzo-draftruntraces");
        if (await traces.ExistsAsync())
        {
            await foreach (var blob in traces.GetBlobsAsync())
            {
                await traces.DeleteBlobIfExistsAsync(blob.Name);
            }
        }

        return await AzureStorageDraftRunTraceStore.ConnectAsync(blobService);
    }
}