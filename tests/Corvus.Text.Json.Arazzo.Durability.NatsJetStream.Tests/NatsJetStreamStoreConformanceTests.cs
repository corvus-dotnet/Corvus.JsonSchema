// <copyright file="NatsJetStreamStoreConformanceTests.cs" company="Endjin Limited">
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
/// Runs the shared store-conformance suite against <see cref="NatsJetStreamWorkflowStateStore"/> over a real
/// NATS server (with JetStream) in a container. Each test gets an empty store (the buckets are dropped).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamStoreConformanceTests : WorkflowStateStoreConformance
{
    private static NatsContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new NatsBuilder().WithImage("nats:2.11").WithCommand("-js").Build();
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

    protected override async ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string url = container.GetConnectionString();

        await using (var connection = new NatsConnection(NatsOpts.Default with { Url = url }))
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            foreach (string bucket in new[] { "arazzo_runs", "arazzo_leases" })
            {
                try
                {
                    await kv.DeleteStoreAsync(bucket);
                }
                catch (NatsJSApiException)
                {
                    // The bucket (JetStream stream) does not exist yet — nothing to reset.
                }
            }
        }

        return await NatsJetStreamWorkflowStateStore.CreateAsync(url, timeProvider);
    }
}