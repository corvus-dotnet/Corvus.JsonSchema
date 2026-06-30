// <copyright file="AzureStorageEnvironmentRunnerAuthorizationStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentRunnerAuthorizationStoreConformance"/> suite against
/// <see cref="AzureStorageEnvironmentRunnerAuthorizationStore"/> over the Azurite emulator in a container. Each test gets an
/// empty store (the runner-authorizations table is provisioned and any residual entities removed).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageEnvironmentRunnerAuthorizationStoreConformanceTests : EnvironmentRunnerAuthorizationStoreConformance
{
    private const string AuthorizationsTable = "arazzoEnvironmentRunnerAuthorizations";
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
    protected override async ValueTask<IEnvironmentRunnerAuthorizationStore> CreateStoreAsync()
    {
        var tableService = new TableServiceClient(container.GetConnectionString());
        await AzureStorageEnvironmentRunnerAuthorizationStore.PrepareAsync(tableService);

        TableClient client = tableService.GetTableClient(AuthorizationsTable);
        await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
        {
            await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All);
        }

        return await AzureStorageEnvironmentRunnerAuthorizationStore.ConnectAsync(tableService);
    }
}