// <copyright file="RedisWorkflowAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowAdministratorStoreConformance"/> suite against
/// <see cref="RedisWorkflowAdministratorStore"/> over a real Redis server in a container. Each test gets an empty store
/// (the database is flushed).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisWorkflowAdministratorStoreConformanceTests : WorkflowAdministratorStoreConformance
{
    private static RedisContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new RedisBuilder().WithImage("redis:7-alpine").Build();
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

    protected override async ValueTask<IWorkflowAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string configuration = container.GetConnectionString();

        await using (var admin = await ConnectionMultiplexer.ConnectAsync($"{configuration},allowAdmin=true"))
        {
            await admin.GetServer(admin.GetEndPoints()[0]).FlushDatabaseAsync();
        }

        await RedisWorkflowAdministratorStore.PrepareAsync(configuration);
        return await RedisWorkflowAdministratorStore.ConnectAsync(configuration, timeProvider);
    }
}