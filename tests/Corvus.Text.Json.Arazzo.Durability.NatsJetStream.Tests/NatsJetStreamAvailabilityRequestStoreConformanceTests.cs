// <copyright file="NatsJetStreamAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Testcontainers.Nats;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against
/// <see cref="NatsJetStreamAvailabilityRequestStore"/> over a real NATS server (with JetStream) in a container. Each
/// test gets an empty store (the KV bucket is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        try
        {
            await kv.DeleteStoreAsync("arazzo_availability_requests");
        }
        catch (NatsJSApiException)
        {
            // The bucket (JetStream stream) does not exist yet — nothing to reset.
        }

        await NatsJetStreamAvailabilityRequestStore.PrepareAsync(connection);
        return await NatsJetStreamAvailabilityRequestStore.ConnectAsync(connection, timeProvider);
    }
}