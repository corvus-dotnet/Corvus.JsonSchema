// <copyright file="MongoSourceStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="SourceStoreConformance"/> suite against <see cref="MongoSourceStore"/> over a
/// real MongoDB server in a container. Each test gets an empty store (the collection is dropped and the indexes
/// re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoSourceStoreConformanceTests : SourceStoreConformance
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
    protected override async ValueTask<ISourceStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await client.GetDatabase(DatabaseName).DropCollectionAsync("sources");

        // Provision the indexes then open for operation over the caller-owned client.
        await MongoSourceStore.PrepareAsync(client, DatabaseName);
        return await MongoSourceStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}