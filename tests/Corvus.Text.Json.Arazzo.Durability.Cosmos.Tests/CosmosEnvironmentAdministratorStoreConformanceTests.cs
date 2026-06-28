// <copyright file="CosmosEnvironmentAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.CosmosDb;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentAdministratorStoreConformance"/> suite against
/// <see cref="CosmosEnvironmentAdministratorStore"/> over the Azure Cosmos DB emulator in a container. Each test gets an
/// empty store (the database is dropped and recreated).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("cosmos")]
public sealed class CosmosEnvironmentAdministratorStoreConformanceTests : EnvironmentAdministratorStoreConformance
{
    private const string DatabaseName = "arazzo";
    private static CosmosDbContainer container = null!;
    private static CosmosClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new CosmosDbBuilder().Build();
        await container.StartAsync();

        CosmosClientOptions options = CosmosEnvironmentAdministratorStore.CreateClientOptions();
        options.ConnectionMode = ConnectionMode.Gateway;
        options.HttpClientFactory = () => container.HttpClient;
        options.LimitToEndpoint = true;
        client = new CosmosClient(container.GetConnectionString(), options);
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        client?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override async ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        try
        {
            await client.GetDatabase(DatabaseName).DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Nothing to reset on the first run.
        }

        await CosmosEnvironmentAdministratorStore.PrepareAsync(client, DatabaseName);
        return await CosmosEnvironmentAdministratorStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}