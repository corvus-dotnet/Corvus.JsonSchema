// <copyright file="AzureStorageWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against
/// <see cref="AzureStorageWorkspaceWorkflowStore"/> over the Azurite emulator in a container. Each test gets an empty
/// store (the working-copies blob container and index table are both drained and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
{
    private const string WorkspaceWorkflowsTable = "arazzoWorkspaceWorkflows";
    private const string WorkspaceWorkflowsContainer = "arazzo-workspace-workflows";
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

    /// <inheritdoc/>
    protected override async ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        // Pin the blob API version the Azurite emulator supports (its default is the newest, which Azurite rejects).
        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        var tableService = new TableServiceClient(connectionString);
        await AzureStorageWorkspaceWorkflowStore.PrepareAsync(blobService, tableService);

        // Drain the index table and the document container so each test starts empty.
        TableClient table = tableService.GetTableClient(WorkspaceWorkflowsTable);
        await foreach (TableEntity entity in table.QueryAsync<TableEntity>())
        {
            await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
        }

        BlobContainerClient blobs = blobService.GetBlobContainerClient(WorkspaceWorkflowsContainer);
        await foreach (BlobItem blob in blobs.GetBlobsAsync())
        {
            await blobs.DeleteBlobIfExistsAsync(blob.Name);
        }

        return await AzureStorageWorkspaceWorkflowStore.ConnectAsync(blobService, tableService, timeProvider);
    }
}