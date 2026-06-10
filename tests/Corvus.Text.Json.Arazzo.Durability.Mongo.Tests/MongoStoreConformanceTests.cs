// <copyright file="MongoStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared store-conformance suite against <see cref="MongoWorkflowStateStore"/> over a real MongoDB
/// server in a container. Each test gets an empty store (the database is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoStoreConformanceTests : WorkflowStateStoreConformance
{
    private const string DatabaseName = "arazzo";
    private static MongoDbContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MongoDbBuilder().WithImage("mongo:7").Build();
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

    protected override async ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();
        var client = new MongoClient(connectionString);
        await client.DropDatabaseAsync(DatabaseName);

        return await MongoWorkflowStateStore.CreateAsync(connectionString, DatabaseName, timeProvider);
    }
}