// <copyright file="AzureStorageScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="AzureStorageScheduleRegistry"/>
/// over the Azurite emulator in a container. Each test gets an empty registry (the table's entities are
/// deleted).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageScheduleRegistryConformanceTests : ScheduleRegistryConformance
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

    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
    {
        string connectionString = container.GetConnectionString();
        var tableService = new TableServiceClient(connectionString);

        await AzureStorageScheduleRegistry.PrepareAsync(tableService);

        TableClient table = tableService.GetTableClient("arazzoschedules");
        await foreach (TableEntity entity in table.QueryAsync<TableEntity>())
        {
            await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
        }

        return await AzureStorageScheduleRegistry.ConnectAsync(tableService);
    }
}