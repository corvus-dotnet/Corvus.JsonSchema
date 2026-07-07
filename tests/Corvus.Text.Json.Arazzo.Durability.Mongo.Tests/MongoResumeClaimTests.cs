// <copyright file="MongoResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MongoDB.Driver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="MongoWorkflowStateStore"/> over a real MongoDB server in a container. Each test gets an empty
/// store (the database is dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoResumeClaimTests
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

    [TestMethod]
    public async Task Surfaces_only_the_resume_requested_runs()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Surfaces_only_the_resume_requested_runs(store);
    }

    [TestMethod]
    public async Task Respects_hosted_and_environment_filters()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Respects_hosted_and_environment_filters(store);
    }

    private static async ValueTask<IWorkflowStateStore> CreateStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);

        // Provision the indexes then open for operation over the caller-owned client.
        await MongoWorkflowStateStore.PrepareAsync(client, DatabaseName);
        return await MongoWorkflowStateStore.ConnectAsync(client, DatabaseName);
    }
}