// <copyright file="MongoWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against <see cref="MongoWorkspaceWorkflowStore"/>
/// over a real MongoDB server in a container. Each test gets an empty store (the collection is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        // The working-copy store keys and pages purely on the automatic _id index, so there is nothing to provision —
        // dropping the collection is a fresh, empty store.
        await client.GetDatabase(DatabaseName).DropCollectionAsync("workspaceWorkflows");
        return await MongoWorkspaceWorkflowStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}