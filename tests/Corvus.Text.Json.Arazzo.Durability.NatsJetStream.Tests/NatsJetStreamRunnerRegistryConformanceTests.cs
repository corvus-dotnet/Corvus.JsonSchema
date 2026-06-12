// <copyright file="NatsJetStreamRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Testcontainers.Nats;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Runs the shared registry-conformance suite against <see cref="NatsJetStreamRunnerRegistry"/> over a real
/// NATS server (with JetStream) in a container, exercising the connection (caller-owned) overloads. Each test
/// gets an empty registry (the bucket is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamRunnerRegistryConformanceTests : RunnerRegistryConformance
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

    protected override async ValueTask<IRunnerRegistry> CreateRegistryAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        try
        {
            await kv.DeleteStoreAsync("arazzo_runners");
        }
        catch (NatsJSApiException)
        {
            // The bucket (JetStream stream) does not exist yet — nothing to reset.
        }

        // Provision the bucket then open for operation over the caller-owned connection.
        await NatsJetStreamRunnerRegistry.PrepareAsync(connection);
        return await NatsJetStreamRunnerRegistry.ConnectAsync(connection);
    }
}