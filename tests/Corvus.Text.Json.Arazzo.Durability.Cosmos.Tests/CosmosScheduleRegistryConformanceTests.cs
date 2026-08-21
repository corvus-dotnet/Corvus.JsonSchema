// <copyright file="CosmosScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.CosmosDb;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="CosmosScheduleRegistry"/> over the
/// Azure Cosmos DB emulator in a container. Each test gets an empty registry (the database is deleted and
/// re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("cosmos")]
public sealed class CosmosScheduleRegistryConformanceTests : ScheduleRegistryConformance
{
    private const string DatabaseName = "arazzo";
    private static CosmosDbContainer container = null!;
    private static CosmosClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new CosmosDbBuilder().Build();
        await container.StartAsync();

        // The emulator uses a self-signed certificate and only supports gateway mode; the container exposes a
        // pre-configured HttpClient that trusts it.
        CosmosClientOptions options = CosmosWorkflowStateStore.CreateClientOptions();
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

    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
    {
        try
        {
            await client.GetDatabase(DatabaseName).DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Nothing to reset on the first run.
        }

        // Provision (management plane) then open for operation (data plane) over the same client.
        await CosmosScheduleRegistry.PrepareAsync(client, DatabaseName);
        return await CosmosScheduleRegistry.ConnectAsync(client, DatabaseName);
    }
}