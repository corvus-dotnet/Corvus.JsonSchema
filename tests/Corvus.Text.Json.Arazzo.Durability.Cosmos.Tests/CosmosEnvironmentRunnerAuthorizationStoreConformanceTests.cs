// <copyright file="CosmosEnvironmentRunnerAuthorizationStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.CosmosDb;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentRunnerAuthorizationStoreConformance"/> suite against
/// <see cref="CosmosEnvironmentRunnerAuthorizationStore"/> over the Azure Cosmos DB emulator in a container. Each test gets
/// an empty store (the database is dropped and recreated).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("cosmos")]
public sealed class CosmosEnvironmentRunnerAuthorizationStoreConformanceTests : EnvironmentRunnerAuthorizationStoreConformance
{
    private const string DatabaseName = "arazzo";
    private static CosmosDbContainer container = null!;
    private static CosmosClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new CosmosDbBuilder().Build();
        await container.StartAsync();

        CosmosClientOptions options = CosmosEnvironmentRunnerAuthorizationStore.CreateClientOptions();
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

    /// <inheritdoc/>
    protected override async ValueTask<IEnvironmentRunnerAuthorizationStore> CreateStoreAsync()
    {
        try
        {
            await client.GetDatabase(DatabaseName).DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Nothing to reset on the first run.
        }

        await CosmosEnvironmentRunnerAuthorizationStore.PrepareAsync(client, DatabaseName);
        return await CosmosEnvironmentRunnerAuthorizationStore.ConnectAsync(client, DatabaseName);
    }
}