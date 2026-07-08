// <copyright file="AzureStorageWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against
/// <see cref="AzureStorageWorkspaceWorkflowStore"/> over the Azurite emulator in a container. Each test gets an empty
/// store (the working-copies table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
{
    private const string WorkspaceWorkflowsTable = "arazzoWorkspaceWorkflows";
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
        var tableService = new TableServiceClient(container.GetConnectionString());
        await AzureStorageWorkspaceWorkflowStore.PrepareAsync(tableService);

        TableClient client = tableService.GetTableClient(WorkspaceWorkflowsTable);
        await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
        {
            await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
        }

        return await AzureStorageWorkspaceWorkflowStore.ConnectAsync(tableService, timeProvider);
    }
}