// <copyright file="AzureStorageResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="AzureStorageWorkflowStateStore"/> over the Azurite emulator in a container. Each test gets an
/// empty store (the container's blobs and the tables' entities are cleared).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageResumeClaimTests
{
    private static AzuriteContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
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

    [TestMethod]
    public async Task Surfaces_only_the_resume_requested_runs()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Surfaces_only_the_resume_requested_runs(store);
    }

    [TestMethod]
    public async Task Respects_hosted_and_environment_filters()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Respects_hosted_and_environment_filters(store);
    }

    private static async ValueTask<IWorkflowStateStore> CreateStoreAsync()
    {
        string connectionString = container.GetConnectionString();

        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        var tableService = new TableServiceClient(connectionString);

        // Provision the container and tables, then reset data, then open for operation.
        await AzureStorageWorkflowStateStore.PrepareAsync(blobService, tableService);

        BlobContainerClient runs = blobService.GetBlobContainerClient("arazzo-runs");
        if (await runs.ExistsAsync())
        {
            await foreach (var blob in runs.GetBlobsAsync())
            {
                await runs.DeleteBlobIfExistsAsync(blob.Name);
            }
        }

        foreach (string table in new[] { "arazzoindex", "arazzoleases" })
        {
            TableClient client = tableService.GetTableClient(table);
            try
            {
                await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
                {
                    await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // The table does not exist yet — nothing to reset.
            }
        }

        return await AzureStorageWorkflowStateStore.ConnectAsync(blobService, tableService);
    }
}