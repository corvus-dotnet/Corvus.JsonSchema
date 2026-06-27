// <copyright file="AzureStorageWorkflowAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowAdministratorStoreConformance"/> suite against
/// <see cref="AzureStorageWorkflowAdministratorStore"/> over the Azurite emulator in a container. Each test gets an empty
/// store (the administrators table is provisioned and any residual entities removed).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageWorkflowAdministratorStoreConformanceTests : WorkflowAdministratorStoreConformance
{
    private const string AdministratorsTable = "arazzoWorkflowAdministrators";
    private const string IndexTable = "arazzoWorkflowAdministratorIndex";
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

    protected override async ValueTask<IWorkflowAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        var tableService = new TableServiceClient(container.GetConnectionString());
        await AzureStorageWorkflowAdministratorStore.PrepareAsync(tableService);

        foreach (string table in new[] { AdministratorsTable, IndexTable })
        {
            TableClient client = tableService.GetTableClient(table);
            await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
            {
                await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
            }
        }

        return await AzureStorageWorkflowAdministratorStore.ConnectAsync(tableService, timeProvider);
    }
}