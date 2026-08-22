// <copyright file="MongoScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="MongoScheduleRegistry"/> over a real
/// MongoDB server in a container. Each test gets an empty registry (the database is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoScheduleRegistryConformanceTests : ScheduleRegistryConformance
{
    private const string DatabaseName = "arazzo";
    private static MongoDbContainer container = null!;
    private static IMongoClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MongoDbBuilder().Build();
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

    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        return await MongoScheduleRegistry.ConnectAsync(client, DatabaseName);
    }
}