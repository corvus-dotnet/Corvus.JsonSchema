// <copyright file="AzureStorageCatalogStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared catalog-conformance suite against <see cref="AzureStorageWorkflowCatalogStore"/> over the
/// Azurite emulator in a container. Each test gets an empty store (the container's blobs and the table's
/// entities are cleared).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageCatalogStoreConformanceTests : WorkflowCatalogStoreConformance
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

    protected override async ValueTask<IWorkflowCatalogStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        // Build the service clients the caller owns (here from the Azurite connection string; in production
        // these would carry a managed identity / TokenCredential). The Blob version is pinned to one Azurite
        // accepts. The store is then provisioned and opened over these caller-supplied clients.
        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        var tableService = new TableServiceClient(connectionString);

        // Provision the container and table (the admin/DDL step), then reset data, then open for operation.
        await AzureStorageWorkflowCatalogStore.PrepareAsync(blobService, tableService);

        BlobContainerClient packages = blobService.GetBlobContainerClient("arazzo-catalog");
        if (await packages.ExistsAsync())
        {
            await foreach (var blob in packages.GetBlobsAsync())
            {
                await packages.DeleteBlobIfExistsAsync(blob.Name);
            }
        }

        TableClient catalog = tableService.GetTableClient("arazzocatalog");
        try
        {
            await foreach (TableEntity entity in catalog.QueryAsync<TableEntity>())
            {
                await catalog.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The table does not exist yet — nothing to reset.
        }

        return await AzureStorageWorkflowCatalogStore.ConnectAsync(blobService, tableService, timeProvider);
    }
}