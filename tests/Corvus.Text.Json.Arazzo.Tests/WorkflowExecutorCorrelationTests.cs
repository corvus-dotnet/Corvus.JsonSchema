// <copyright file="WorkflowExecutorCorrelationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.AsyncApi.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Coverage of an AsyncAPI receive step's <c>correlationId</c> (Arazzo 1.1): a per-execution register
/// links the correlation token a prior send published (read from its payload at the AsyncAPI correlation
/// id's <c>location</c>) to the response a later receive step waits for — the receive accepts only the
/// message carrying that token.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string CorrelationDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "correlate",
              "steps": [
                {
                  "stepId": "ask",
                  "channelPath": "notifications",
                  "action": "send",
                  "requestBody": { "payload": { "correlationId": "abc-123", "data": "hi" } }
                },
                {
                  "stepId": "listen",
                  "channelPath": "replies",
                  "action": "receive",
                  "correlationId": "corr",
                  "outputs": { "v": "$message.payload#/v" }
                }
              ],
              "outputs": { "v": "$steps.listen.outputs.v" }
            }
          ]
        }
        """;

    [TestMethod]
    public void CorrelationToken_matches_only_the_expected_token()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"correlationId":"abc-123","v":42}"""));
        JsonElement message = doc.RootElement;

        CorrelationToken.Matches(message, "/correlationId"u8, "abc-123"u8).ShouldBeTrue();
        CorrelationToken.Matches(message, "/correlationId"u8, "other"u8).ShouldBeFalse();
        CorrelationToken.Matches(message, "/missing"u8, "abc-123"u8).ShouldBeFalse();   // pointer does not resolve
        CorrelationToken.Matches(message, "/v"u8, "42"u8).ShouldBeFalse();             // resolves to a non-string value

        CorrelationToken.TryRead(message, "/correlationId"u8, out byte[] token).ShouldBeTrue();
        Encoding.UTF8.GetString(token).ShouldBe("abc-123");
        CorrelationToken.TryRead(message, "/v"u8, out _).ShouldBeFalse();              // non-string value
        CorrelationToken.TryRead(message, "/missing"u8, out _).ShouldBeFalse();        // pointer does not resolve
    }

    [TestMethod]
    public void Emits_correlation_capture_on_send_and_filter_on_receive()
    {
        string source = EmitCorrelation(CorrelationDocument);

        // A per-execution register links send→receive by correlation id name.
        source.ShouldContain("correlationTokens = new(System.StringComparer.Ordinal)");

        // The send captures the token from its published payload at the correlation location.
        source.ShouldContain("CorrelationToken.TryRead(");
        source.ShouldContain("\"/correlationId\"u8");
        source.ShouldContain("correlationTokens[\"corr\"]");

        // The receive only accepts the message carrying the registered token.
        source.ShouldContain("correlationTokens.TryGetValue(\"corr\"");
        source.ShouldContain("CorrelationToken.Matches(JsonElement.From(message), \"/correlationId\"u8");
    }

    [TestMethod]
    public void Correlation_on_a_send_step_is_rejected()
    {
        // Add a correlationId to the send step (JSON is whitespace-insensitive, so an inline insert is fine).
        string document = CorrelationDocument.Replace(
            "\"action\": \"send\",",
            "\"action\": \"send\", \"correlationId\": \"corr\",",
            StringComparison.Ordinal);

        NotSupportedException ex = Should.Throw<NotSupportedException>(() => EmitCorrelation(document));
        ex.Message.ShouldContain("receive");
    }

    [TestMethod]
    public void Correlation_with_an_unknown_name_is_rejected()
    {
        string document = CorrelationDocument.Replace("\"correlationId\": \"corr\"", "\"correlationId\": \"nope\"", StringComparison.Ordinal);

        NotSupportedException ex = Should.Throw<NotSupportedException>(() => EmitCorrelation(document));
        ex.Message.ShouldContain("nope");
    }

    [TestMethod]
    public void Correlation_with_a_header_location_is_rejected()
    {
        // The correlation id resolves to a header location, which the send step cannot populate.
        NotSupportedException ex = Should.Throw<NotSupportedException>(
            () => EmitCorrelation(CorrelationDocument, correlationLocation: "$message.header#/correlationId"));
        ex.Message.ShouldContain("payload");
    }

    [TestMethod]
    public async Task Generated_executor_filters_received_messages_by_correlation()
    {
        string source = EmitCorrelation(CorrelationDocument);

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.CorrelateWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        // The send runs synchronously and registers token "abc-123"; the receive then subscribes to "replies".
        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        // A message carrying the wrong correlation token is ignored; the receive keeps waiting...
        await messageTransport.DeliverAsync<JsonElement>("replies", Encoding.UTF8.GetBytes("""{"correlationId":"WRONG","v":1}"""));

        // ...until the correlated message arrives, which the step accepts.
        await messageTransport.DeliverAsync<JsonElement>("replies", Encoding.UTF8.GetBytes("""{"correlationId":"abc-123","v":42}"""));

        JsonElement outputs = await pending;
        outputs.TryGetProperty("v"u8, out JsonElement v).ShouldBeTrue();
        v.GetInt32().ShouldBe(42);
    }

    [TestMethod]
    public async Task Generated_executor_filters_by_correlation_in_the_control_flow_loop()
    {
        // An onFailure action on the receive step promotes the workflow into the control-flow loop, so the
        // correlation filter is emitted on the control-flow receive path.
        string document = CorrelationDocument.Replace(
            "\"correlationId\": \"corr\",",
            "\"correlationId\": \"corr\", \"onFailure\": [ { \"name\": \"bail\", \"type\": \"end\" } ],",
            StringComparison.Ordinal);

        string source = EmitCorrelation(document);
        source.ShouldContain("while (true)");
        source.ShouldContain("CorrelationToken.Matches(JsonElement.From(message), \"/correlationId\"u8");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.CorrelateWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        await messageTransport.DeliverAsync<JsonElement>("replies", Encoding.UTF8.GetBytes("""{"correlationId":"WRONG","v":1}"""));
        await messageTransport.DeliverAsync<JsonElement>("replies", Encoding.UTF8.GetBytes("""{"correlationId":"abc-123","v":42}"""));

        JsonElement outputs = await pending;
        outputs.TryGetProperty("v"u8, out JsonElement v).ShouldBeTrue();
        v.GetInt32().ShouldBe(42);
    }

    [TestMethod]
    public async Task Receive_with_no_registered_token_fails_the_step()
    {
        // A receive declares correlationId but no prior step published a correlated request, so the register
        // holds no token and the step fails rather than waiting for a message that can never correlate.
        string document = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
              "workflows": [
                {
                  "workflowId": "correlate",
                  "steps": [
                    { "stepId": "listen", "channelPath": "replies", "action": "receive", "correlationId": "corr", "outputs": { "v": "$message.payload#/v" } }
                  ],
                  "outputs": { "v": "$steps.listen.outputs.v" }
                }
              ]
            }
            """;

        string source = EmitCorrelation(document);

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.CorrelateWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        WorkflowStepFailedException? caught = null;
        try
        {
            _ = await pending;
        }
        catch (WorkflowStepFailedException ex)
        {
            caught = ex;
        }

        caught.ShouldNotBeNull();
        caught!.Message.ShouldContain("correlation");
    }

    private static string EmitCorrelation(string document, string correlationLocation = "$message.payload#/correlationId", bool durable = false)
    {
        var send = new AsyncApiChannelDescriptor(
            "notifications",
            OperationAction.Send,
            "onNotify",
            ProducerClassName: "Acme.Notifications.NotifyProducer",
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("notify", "Corvus.Text.Json.JsonElement", null, null, "PublishNotifyAsync", null, "corr", correlationLocation)]);

        var receive = new AsyncApiChannelDescriptor(
            "replies",
            OperationAction.Receive,
            "onReply",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("reply", "Corvus.Text.Json.JsonElement", null, null, null, null, "corr", correlationLocation)]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [send, receive])]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("GeneratedWorkflows", "CorrelateWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement", null, durable));
    }

    [TestMethod]
    public void Durable_correlated_receive_suspends_on_a_message_wait()
    {
        // A durable correlated receive: with a run present it either consumes a worker-delivered message or
        // checkpoints a message wait (guarded by a registered correlation token) and returns Suspended.
        string source = EmitCorrelation(CorrelationDocument, durable: true);

        source.ShouldContain("if (run.TryTakeDeliveredMessage(out JsonElement");
        source.ShouldContain("else if (correlationTokens.TryGetValue(\"corr\"");
        source.ShouldContain("await run.SuspendForMessageAsync(");
        source.ShouldContain(".Suspended(");

        // Compiles (the durable correlated receive shape is valid C#).
        Assembly assembly = CompileInMemory(source);
        assembly.GetType("GeneratedWorkflows.CorrelateWorkflow").ShouldNotBeNull();
    }
}