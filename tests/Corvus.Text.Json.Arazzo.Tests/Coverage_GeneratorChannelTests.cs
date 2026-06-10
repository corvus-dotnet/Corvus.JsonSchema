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
    public void Send_to_parameterised_channel_passes_the_channel_parameter()
    {
        AsyncApiChannelDescriptor descriptor = new(
            "orders/{region}",
            OperationAction.Send,
            "place",
            "Gen.Events.PlaceProducer",
            IsDynamicAddress: false,
            ChannelParameters: ["region"],
            Messages: [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, "PublishOrderAsync")]);

        string source = EmitSend(
            descriptor,
            """{ "stepId": "s", "channelPath": "orders/{region}", "action": "send", "requestBody": { "payload": "$inputs.m" }, "parameters": [ { "name": "region", "value": "$inputs.region" } ] }""");

        source.ShouldContain(".GetString();");
        source.ShouldContain(".PublishOrderAsync(");
    }

    [TestMethod]
    public void Receive_from_parameterised_channel_builds_the_address()
    {
        AsyncApiChannelDescriptor descriptor = new(
            "measurements/{sensorId}",
            OperationAction.Receive,
            "onMeasured",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: ["sensorId"],
            Messages: [new AsyncApiChannelMessageDescriptor("m", "Corvus.Text.Json.JsonElement", null, null, null)]);

        string source = EmitReceive(
            descriptor,
            """{ "stepId": "s", "channelPath": "measurements/{sensorId}", "action": "receive", "parameters": [ { "name": "sensorId", "value": "$inputs.sensorId" } ] }""");

        source.ShouldContain("$\"measurements/");
        source.ShouldContain("ReceiveOneAsync<");
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

    // A request/reply send descriptor: the producer exposes a SendAndReceive method returning a reply.
    private static AsyncApiChannelDescriptor SendReply(string? requestReplyMethod = "SendAndReceiveOrderAsync")
        => new(
            "ch",
            OperationAction.Send,
            "ask",
            "Gen.Events.PlaceProducer",
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, "PublishOrderAsync", requestReplyMethod)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

    // A request/reply receive descriptor: a responder step replies with its requestBody.
    private static AsyncApiChannelDescriptor Responder()
        => new(
            "ch",
            OperationAction.Receive,
            "onAsk",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("ask", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

    private static string ReceiveStep(string body)
        => "{ \"stepId\": \"s\", \"channelPath\": \"ch\", \"action\": \"receive\", \"requestBody\": { \"payload\": " + body + " } }";

    private static string SendStep(string body)
        => "{ \"stepId\": \"s\", \"channelPath\": \"ch\", \"action\": \"send\", \"requestBody\": { \"payload\": " + body + " } }";

    [TestMethod]
    public void Send_with_null_producer_class_is_not_supported()
    {
        AsyncApiChannelDescriptor d = new(
            "ch",
            OperationAction.Send,
            "place",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, "PublishOrderAsync")]);

        Should.Throw<NotSupportedException>(() => EmitSend(d, SendStep("\"$inputs.m\"")));
    }

    [TestMethod]
    public void Send_with_message_missing_producer_method_is_not_supported()
    {
        AsyncApiChannelDescriptor d = Send(messages: [new AsyncApiChannelMessageDescriptor("order", "Gen.Events.Order", null, null, null)]);
        Should.Throw<NotSupportedException>(() => EmitSend(d, SendStep("\"$inputs.m\"")));
    }

    [TestMethod]
    public void Send_request_reply_with_missing_method_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitSend(SendReply(requestReplyMethod: null), SendStep("\"$inputs.m\"")));
    }

    [TestMethod]
    public void Send_bakes_constant_payloads_for_literal_kinds()
    {
        foreach (string payload in new[] { "\"hi\"", "42", "true", "null" })
        {
            string source = EmitSend(Send(), SendStep(payload));
            source.ShouldContain("ParsedJsonDocument<JsonElement>");
            source.ShouldContain(".PublishOrderAsync(");
        }
    }

    [TestMethod]
    public void Send_request_reply_gates_on_success_criteria()
    {
        string source = EmitSend(
            SendReply(),
            """{ "stepId": "s", "channelPath": "ch", "action": "send", "requestBody": { "payload": "$inputs.m" }, "successCriteria": [ { "condition": "$message.payload#/ok == 'yes'" } ] }""");
        source.ShouldContain(".SendAndReceiveOrderAsync(");
        source.ShouldContain("did not satisfy its success criteria");
    }

    [TestMethod]
    public void Send_request_reply_without_outputs_clones_the_whole_reply()
    {
        string source = EmitSend(SendReply(), SendStep("\"$inputs.m\""));
        source.ShouldContain(".SendAndReceiveOrderAsync(");
        source.ShouldContain(".CloneAsBuilder(");
    }

    [TestMethod]
    public void Responder_reply_bakes_constant_literal_kinds()
    {
        foreach (string payload in new[] { "\"done\"", "99", "false", "null" })
        {
            string source = EmitReceive(Responder(), ReceiveStep(payload));
            source.ShouldContain("ReceiveOneAndReplyAsync<");
            source.ShouldContain("ParsedJsonDocument<JsonElement>");
        }
    }

    [TestMethod]
    public void Responder_composite_reply_with_nested_interpolation_is_built()
    {
        string source = EmitReceive(Responder(), ReceiveStep("""{ "g": "hi-{$message.payload#/n}" }"""));
        source.ShouldContain("ReceiveOneAndReplyAsync<");
        source.ShouldContain("Interpolation.AppendUtf8");
    }

    [TestMethod]
    public void Responder_composite_reply_with_unresolvable_field_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitReceive(Responder(), ReceiveStep("""{ "x": "$url" }""")));
    }

    [TestMethod]
    public void Responder_interpolation_reply_with_unclosed_brace_is_literal_remainder()
    {
        string source = EmitReceive(Responder(), ReceiveStep("\"a-{$message.payload\""));
        source.ShouldContain("ReceiveOneAndReplyAsync<");
    }

    [TestMethod]
    public void Responder_reply_with_dotted_navigation_resolves()
    {
        string source = EmitReceive(Responder(), ReceiveStep("\"$message.payload.inner\""));
        source.ShouldContain("ReceiveOneAndReplyAsync<");
    }

    [TestMethod]
    public void Receive_with_forbidden_criterion_is_not_supported()
    {
        Should.Throw<NotSupportedException>(() => EmitReceive(
            Responder(),
            """{ "stepId": "s", "channelPath": "ch", "action": "receive", "successCriteria": [ { "condition": "$statusCode == 200" } ], "requestBody": { "payload": "$message.payload" } }"""));
    }

    [TestMethod]
    public void Receive_projects_a_message_header_output_with_a_pointer()
    {
        AsyncApiChannelDescriptor receive = new(
            "ch",
            OperationAction.Receive,
            "onEvent",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("evt", "Corvus.Text.Json.JsonElement", null, null, null)]);

        string source = EmitReceive(
            receive,
            """{ "stepId": "s", "channelPath": "ch", "action": "receive", "outputs": { "v": "$message.header.meta#/sub" } }""");
        source.ShouldContain("messageHeaders.TryGetProperty(\"meta\"u8");
        source.ShouldContain("TryResolvePointer(\"/sub\"u8");
    }

    [TestMethod]
    public void Send_request_reply_with_dynamic_criterion_uses_context()
    {
        // A regex criterion with a dynamic pattern cannot be inlined, so it resolves through the context.
        string source = EmitSend(
            SendReply(),
            """{ "stepId": "s", "channelPath": "ch", "action": "send", "requestBody": { "payload": "$inputs.m" }, "successCriteria": [ { "context": "$message.payload#/name", "type": "regex", "condition": "^{$inputs.prefix}" } ] }""");
        source.ShouldContain("context.SetMessagePayload");
    }

    [TestMethod]
    public void Responder_reply_with_indexed_navigation_resolves()
    {
        string source = EmitReceive(Responder(), ReceiveStep("\"$message.payload[0]\""));
        source.ShouldContain("ReceiveOneAndReplyAsync<");
    }

    [TestMethod]
    public void Responder_interpolation_reply_with_trailing_literal_is_built()
    {
        string source = EmitReceive(Responder(), ReceiveStep("\"{$message.payload#/n}-end\""));
        source.ShouldContain("ReceiveOneAndReplyAsync<");
    }

    [TestMethod]
    public void Send_payload_with_input_pointer_navigates()
    {
        string source = EmitSend(Send(), SendStep("\"$inputs.m#/inner\""));
        source.ShouldContain(".PublishOrderAsync(");
    }

    [TestMethod]
    public void Send_in_control_flow_loop_publishes()
    {
        string source = EmitSend(
            Send(),
            """{ "stepId": "s", "channelPath": "ch", "action": "send", "requestBody": { "payload": "$inputs.m" }, "onSuccess": [ { "name": "stop", "type": "end" } ] }""");
        source.ShouldContain("while (true)");
        source.ShouldContain(".PublishOrderAsync(");
    }

    [TestMethod]
    public void Receive_in_control_flow_loop_with_dynamic_criterion_and_no_outputs()
    {
        AsyncApiChannelDescriptor receive = new(
            "ch",
            OperationAction.Receive,
            "onEvent",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("evt", "Corvus.Text.Json.JsonElement", null, null, null)]);

        string source = EmitReceive(
            receive,
            """{ "stepId": "s", "channelPath": "ch", "action": "receive", "successCriteria": [ { "context": "$message.payload#/name", "type": "regex", "condition": "^{$inputs.prefix}" } ], "onFailure": [ { "name": "again", "type": "retry", "retryAfter": 0, "retryLimit": 2 } ] }""");
        source.ShouldContain("while (true)");
        source.ShouldContain("context.SetMessagePayload");
        source.ShouldContain("CloneAsBuilder(workspace)");
    }
}
