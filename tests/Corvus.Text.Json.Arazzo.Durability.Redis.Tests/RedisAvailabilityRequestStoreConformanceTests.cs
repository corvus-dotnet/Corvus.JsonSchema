// <copyright file="RedisAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against <see cref="RedisAvailabilityRequestStore"/>
/// over a real Redis server in a container. Each test gets an empty store (the database is flushed).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string configuration = container.GetConnectionString();

        await using (var admin = await ConnectionMultiplexer.ConnectAsync($"{configuration},allowAdmin=true"))
        {
            await admin.GetServer(admin.GetEndPoints()[0]).FlushDatabaseAsync();
        }

        await RedisAvailabilityRequestStore.PrepareAsync(configuration);
        return await RedisAvailabilityRequestStore.ConnectAsync(configuration, timeProvider);
    }
}