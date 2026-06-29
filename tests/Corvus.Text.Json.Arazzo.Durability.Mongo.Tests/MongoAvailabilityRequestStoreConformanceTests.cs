// <copyright file="MongoAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against <see cref="MongoAvailabilityRequestStore"/>
/// over a real MongoDB server in a container. Each test gets an empty store (the availability_requests collection is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
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
    protected override async ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await client.GetDatabase(DatabaseName).DropCollectionAsync("availability_requests");
        return await MongoAvailabilityRequestStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}