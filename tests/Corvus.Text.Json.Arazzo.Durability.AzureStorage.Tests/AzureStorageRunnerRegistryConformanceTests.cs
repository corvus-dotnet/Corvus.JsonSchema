// <copyright file="AzureStorageRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared registry-conformance suite against <see cref="AzureStorageRunnerRegistry"/> over the Azurite
/// emulator in a container. Each test gets an empty registry (the runners table is deleted and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageRunnerRegistryConformanceTests : RunnerRegistryConformance
{
    private const string RunnersTable = "arazzoRunners";
    private const string HostingTable = "arazzoRunnerHosting";

    private static AzuriteContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        // Testcontainers.Azurite still defaults to a very old Azurite image; pin a recent one for parity with
        // the other Azure Storage suites.
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

    protected override async ValueTask<IRunnerRegistry> CreateRegistryAsync()
    {
        string connectionString = container.GetConnectionString();
        var tableService = new TableServiceClient(connectionString);

        // Drop the runners and hosting-index tables to isolate each test, then re-provision and open for operation.
        // Deleting a table can briefly leave it in a "being deleted" state, so retry the recreate until it succeeds.
        await DeleteIfExistsAsync(tableService.GetTableClient(RunnersTable));
        await DeleteIfExistsAsync(tableService.GetTableClient(HostingTable));

        while (true)
        {
            try
            {
                await AzureStorageRunnerRegistry.PrepareAsync(tableService);
                break;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // The prior delete is still settling ("table being deleted"); wait and retry the recreate.
                await Task.Delay(200);
            }
        }

        return await AzureStorageRunnerRegistry.ConnectAsync(tableService);
    }

    private static async Task DeleteIfExistsAsync(TableClient table)
    {
        try
        {
            await table.DeleteAsync();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The table does not exist yet — nothing to delete.
        }
    }
}