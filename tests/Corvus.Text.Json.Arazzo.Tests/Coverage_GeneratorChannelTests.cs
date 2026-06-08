// <copyright file="Coverage_GeneratorChannelTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the channel-step emitters' guard clauses — the <c>NotSupported</c>
/// boundaries (parameterised addresses, missing publishable message, missing/non-expression payload) and
/// the receive payload-type fallback. These are the not-yet-supported edges the happy-path channel
/// end-to-end tests do not reach.
/// </summary>
[TestClass]
public class Coverage_GeneratorChannelTests
{
    private static string EmitSend(AsyncApiChannelDescriptor descriptor, string stepJson)
        => Emit(descriptor, "send", stepJson);

    private static string EmitReceive(AsyncApiChannelDescriptor descriptor, string stepJson)
        => Emit(descriptor, "receive", stepJson);

    private static string Emit(AsyncApiChannelDescriptor descriptor, string action, string stepJson)
    {
        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);
        string doc = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "events", "url": "./e.yaml", "type": "asyncapi" } ],
              "workflows": [ { "workflowId": "w", "steps": [ {{stepJson}} ], "outputs": {} } ]
            }
            """;
        using var parsed = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(doc));
        ArazzoDocument.WorkflowObject workflow = parsed.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("Gen", "Wf", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
    }

    private static AsyncApiChannelDescriptor Send(
        IReadOnlyList<string>? parameters = null,
        IReadOnlyList<AsyncApiChannelMessageDescriptor>? messages = null)
        => new(
            "ch",
            OperationAction.Send,
            "place",
            "Gen.Events.PlaceProducer",
            IsDynamicAddress: false,
            ChannelParameters: parameters ?? [],
            Messages: messages ?? [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, "PublishOrderAsync")]);

    [TestMethod]
    public void Send_to_parameterised_channel_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitSend(
            Send(parameters: ["id"]),
            """{ "stepId": "s", "channelPath": "ch", "action": "send", "requestBody": { "payload": "$inputs.m" } }"""));
    }

    [TestMethod]
    public void Send_with_no_publishable_message_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitSend(
            Send(messages: []),
            """{ "stepId": "s", "channelPath": "ch", "action": "send", "requestBody": { "payload": "$inputs.m" } }"""));
    }

    [TestMethod]
    public void Send_with_no_request_body_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitSend(
            Send(),
            """{ "stepId": "s", "channelPath": "ch", "action": "send" }"""));
    }

    [TestMethod]
    public void Receive_from_parameterised_channel_is_not_supported()
    {
        AsyncApiChannelDescriptor receive = new(
            "ch",
            OperationAction.Receive,
            "onEvent",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: ["id"],
            Messages: [new AsyncApiChannelMessageDescriptor("evt", "Gen.Events.Evt", null, null, null)]);

        Should.Throw<NotSupportedException>(() => EmitReceive(
            receive,
            """{ "stepId": "s", "channelPath": "ch", "action": "receive" }"""));
    }

    [TestMethod]
    public void Receive_with_no_message_falls_back_to_json_element_payload()
    {
        AsyncApiChannelDescriptor receive = new(
            "ch",
            OperationAction.Receive,
            "onEvent",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: []);

        string source = EmitReceive(receive, """{ "stepId": "s", "channelPath": "ch", "action": "receive" }""");
        source.ShouldContain("ReceiveOneAsync<Corvus.Text.Json.JsonElement>(");
    }
}
