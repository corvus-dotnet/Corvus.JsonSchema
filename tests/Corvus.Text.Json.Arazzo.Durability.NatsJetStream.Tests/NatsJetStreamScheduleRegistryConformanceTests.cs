// <copyright file="NatsJetStreamScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Testcontainers.Nats;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="NatsJetStreamScheduleRegistry"/>
/// over a real NATS server (JetStream enabled) in a container. Each test gets an empty registry (the bucket
/// is reset and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamScheduleRegistryConformanceTests : ScheduleRegistryConformance
{
    private static NatsContainer container = null!;
    private static NatsConnection connection = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new NatsBuilder().WithImage("nats:2.11").WithCommand("-js").Build();
        await container.StartAsync();
        connection = new NatsConnection(NatsOpts.Default with { Url = container.GetConnectionString() });
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_schedules"],
            () => NatsJetStreamScheduleRegistry.PrepareAsync(connection));
        return await NatsJetStreamScheduleRegistry.ConnectAsync(connection);
    }
}