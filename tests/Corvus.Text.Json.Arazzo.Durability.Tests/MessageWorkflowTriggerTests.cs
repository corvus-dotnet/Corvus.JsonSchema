// <copyright file="MessageWorkflowTriggerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="MessageWorkflowTrigger"/> — an inbound message starts a fresh run, idempotently.</summary>
[TestClass]
public sealed class MessageWorkflowTriggerTests
{
    [TestMethod]
    public async Task Each_message_starts_a_pending_run_and_redelivery_is_idempotent()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        var transport = new InMemoryMessageTransport();

        var startedIds = new List<WorkflowRunId>();
        WorkflowStartHandler start = async (request, cancellationToken) =>
        {
            WorkflowRunId id = await management.StartIdempotentAsync(
                request.WorkflowId, request.Inputs, request.IdempotencyKey, request.CorrelationId, request.Tags, cancellationToken: cancellationToken);
            startedIds.Add(id);
            return id;
        };

        // Idempotency keyed off the order id carried in the payload.
        var binding = new MessageTriggerBinding(
            WorkflowId: "orders-v1",
            Channel: "orders/created",
            IdempotencyKey: "$message.payload#/orderId");

        await using var trigger = new MessageWorkflowTrigger(transport, start, binding);
        await trigger.StartListeningAsync(default);

        await PublishAsync(transport, "orders/created", """{ "orderId": "A1" }""");
        await PublishAsync(transport, "orders/created", """{ "orderId": "A1" }""");  // redelivery of the same order
        await PublishAsync(transport, "orders/created", """{ "orderId": "A2" }""");

        // The handler ran for all three messages, but the redelivery resolved to the same run id.
        startedIds.Count.ShouldBe(3);
        startedIds.Distinct().Count().ShouldBe(2);
        startedIds[0].ShouldBe(startedIds[1]);

        // Exactly two Pending runs exist in the store.
        using WorkflowRunPage pending = await management.ListAsync(new WorkflowQuery(WorkflowRunStatus.Pending), AccessContext.System, default);
        pending.Runs.Count.ShouldBe(2);

        WorkflowRunDetail? a1 = await management.GetAsync(startedIds[0], AccessContext.System, default);
        a1.ShouldNotBeNull();
        a1.Value.Status.ShouldBe(WorkflowRunStatus.Pending);
    }

    private static async ValueTask PublishAsync(InMemoryMessageTransport transport, string channel, string json)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        await transport.PublishAsync(Encoding.UTF8.GetBytes(channel), doc.RootElement);
    }
}