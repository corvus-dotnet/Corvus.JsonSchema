// <copyright file="MongoWorkflowAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowAdministratorStoreConformance"/> suite against
/// <see cref="MongoWorkflowAdministratorStore"/> over a real MongoDB server in a container. Each test gets an empty store
/// (the collection is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoWorkflowAdministratorStoreConformanceTests : WorkflowAdministratorStoreConformance
{
    private const string DatabaseName = "arazzo";
    private static MongoDbContainer container = null!;
    private static IMongoClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MongoDbBuilder().WithImage("mongo:7").Build();
        await container.StartAsync();
        client = new MongoClient(container.GetConnectionString());
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
        await client.GetDatabase(DatabaseName).DropCollectionAsync("workflowAdministrators");

        // Provision then open for operation over the caller-owned client.
        await MongoWorkflowAdministratorStore.PrepareAsync(client, DatabaseName);
        return await MongoWorkflowAdministratorStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}