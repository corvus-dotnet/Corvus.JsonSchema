// <copyright file="RedisResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="RedisWorkflowStateStore"/> over a real Redis server in a container. Each test gets an empty store
/// (the database is flushed).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisResumeClaimTests
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
        string configuration = container.GetConnectionString();

        await using (var admin = await ConnectionMultiplexer.ConnectAsync($"{configuration},allowAdmin=true"))
        {
            await admin.GetServer(admin.GetEndPoints()[0]).FlushDatabaseAsync();
        }

        await RedisWorkflowStateStore.PrepareAsync(configuration);
        return await RedisWorkflowStateStore.ConnectAsync(configuration);
    }
}