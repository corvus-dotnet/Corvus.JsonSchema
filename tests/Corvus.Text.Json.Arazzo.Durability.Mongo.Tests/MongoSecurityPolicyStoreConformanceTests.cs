// <copyright file="MongoSecurityPolicyStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="SecurityPolicyStoreConformance"/> suite against <see cref="MongoSecurityPolicyStore"/>
/// over a real MongoDB server in a container. Each test gets an empty store (the database is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoSecurityPolicyStoreConformanceTests : SecurityPolicyStoreConformance
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

    protected override async ValueTask<ISecurityPolicyStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await client.DropDatabaseAsync(DatabaseName);
        return await MongoSecurityPolicyStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}