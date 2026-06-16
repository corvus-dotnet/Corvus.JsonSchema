// <copyright file="MongoSourceCredentialStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the shared <see cref="SourceCredentialStoreConformance"/> suite against <see cref="MongoSourceCredentialStore"/>
/// over a real MongoDB server in a container. Each test gets an empty store (the collection is dropped and the indexes
/// re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoSourceCredentialStoreConformanceTests : SourceCredentialStoreConformance
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

    protected override async ValueTask<ISourceCredentialStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await client.GetDatabase(DatabaseName).DropCollectionAsync("source_credentials");

        // Provision the indexes then open for operation over the caller-owned client.
        await MongoSourceCredentialStore.PrepareAsync(client, DatabaseName);
        return await MongoSourceCredentialStore.ConnectAsync(client, DatabaseName, timeProvider);
    }
}