// <copyright file="WorkflowExecutorChannelTests.cs" company="Endjin Limited">
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
/// End-to-end proof that a <c>send</c> AsyncAPI channel step compiles and runs: it publishes the step's
/// payload on a channel through the generated producer and an <see cref="InMemoryMessageTransport"/>.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string ChannelSendDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "notify",
              "steps": [
                {
                  "stepId": "send",
                  "channelPath": "notifications",
                  "action": "send",
                  "requestBody": { "payload": "$inputs.message" }
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_sends_a_message_on_a_channel()
    {
        // A channel descriptor pointing at the test's fake producer (mirrors what DescribeChannelOperations
        // would yield for a static-address send operation).
        var descriptor = new AsyncApiChannelDescriptor(
            "notifications",
            OperationAction.Send,
            "notify",
            "Acme.Notifications.NotifyProducer",
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("notify", "Corvus.Text.Json.JsonElement", null, null, "PublishNotifyAsync")]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelSendDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "NotifyWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The executor takes an IMessageTransport because the workflow has a channel step.
        source.ShouldContain("IMessageTransport messageTransport");
        source.ShouldContain("new Acme.Notifications.NotifyProducer(messageTransport)");
        source.ShouldContain(".PublishNotifyAsync(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.NotifyWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"message":{"text":"hi"}}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await pending;

        // The step published the $inputs.message payload on the 'notifications' channel.
        messageTransport.PublishedMessages.Count.ShouldBe(1);
        messageTransport.PublishedMessages[0].Channel.ShouldBe("notifications");
        Encoding.UTF8.GetString(messageTransport.PublishedMessages[0].PayloadBytes).ShouldContain("hi");
    }

    private const string ChannelReceiveDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listen",
              "steps": [
                {
                  "stepId": "receive",
                  "channelPath": "measurements",
                  "action": "receive"
                }
              ],
              "outputs": { "lumens": "$steps.receive.outputs.lumens" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_receives_a_message_from_a_channel()
    {
        // A receive descriptor whose message payload is delivered as JsonElement (the typed receive path).
        var descriptor = new AsyncApiChannelDescriptor(
            "measurements",
            OperationAction.Receive,
            "onMeasured",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("measured", "Corvus.Text.Json.JsonElement", null, null, null)]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The receive step awaits one typed message (via the ReceiveOneAsync subscriber wrapper) and
        // captures its payload as the step's outputs.
        source.ShouldContain("messageTransport.ReceiveOneAsync<Corvus.Text.Json.JsonElement>(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        // Start the executor: it subscribes synchronously and then awaits the first message. Deliver one
        // so the subscriber wrapper completes, then await the run.
        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"lumens":150}"""));
        JsonElement outputs = await pending;

        // The received payload became the step's outputs and flowed to the workflow output.
        outputs.TryGetProperty("lumens"u8, out JsonElement lumens).ShouldBeTrue();
        lumens.GetInt32().ShouldBe(150);
    }

    private const string ChannelReceiveWithOutputsDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listen",
              "steps": [
                {
                  "stepId": "receive",
                  "channelPath": "measurements",
                  "action": "receive",
                  "outputs": { "celsius": "$message.payload#/temp" }
                }
              ],
              "outputs": { "c": "$steps.receive.outputs.celsius" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_projects_message_payload_outputs()
    {
        var descriptor = new AsyncApiChannelDescriptor(
            "measurements",
            OperationAction.Receive,
            "onMeasured",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("measured", "Corvus.Text.Json.JsonElement", null, null, null)]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveWithOutputsDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The message is bound in the receive handler and only the declared output value is projected.
        source.ShouldContain("messageTransport.ReceiveOneAsync<Corvus.Text.Json.JsonElement>(");
        source.ShouldContain("JsonElement receiveMessagePayload = JsonElement.From(message);");
        source.ShouldContain("receiveMessagePayload.TryResolvePointer(\"/temp\"u8");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"temp":21,"humidity":80}"""));
        JsonElement outputs = await pending;

        // Only the projected field flows through; the rest of the message is not in the outputs.
        outputs.TryGetProperty("c"u8, out JsonElement celsius).ShouldBeTrue();
        celsius.GetInt32().ShouldBe(21);
    }

    private const string ChannelReceiveWithCriteriaDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listen",
              "steps": [
                {
                  "stepId": "receive",
                  "channelPath": "measurements",
                  "action": "receive",
                  "successCriteria": [ { "condition": "$message.payload#/status == 'ready'" } ],
                  "outputs": { "id": "$message.payload#/id" }
                }
              ],
              "outputs": { "id": "$steps.receive.outputs.id" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_gates_a_received_message_on_success_criteria()
    {
        MethodInfo execute = CompileReceiveWithCriteria(out string source);

        // The criterion is inlined against the received payload (no WorkflowExecutionContext).
        source.ShouldContain("receiveMessagePayload.TryResolvePointer(\"/status\"u8");
        source.ShouldNotContain("WorkflowExecutionContext");

        // A message that satisfies the criterion flows its projected output through.
        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"status":"ready","id":"x1"}"""));
        JsonElement outputs = await pending;

        outputs.TryGetProperty("id"u8, out JsonElement id).ShouldBeTrue();
        id.GetString().ShouldBe("x1");
    }

    [TestMethod]
    public async Task Generated_executor_fails_a_received_message_that_misses_its_criteria()
    {
        MethodInfo execute = CompileReceiveWithCriteria(out _);

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        // A message that misses the criterion fails the step. The criterion gate runs inline on the
        // delivering thread, so the failure may surface from DeliverAsync or from awaiting the run.
        WorkflowStepFailedException? caught = null;
        try
        {
            await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"status":"pending","id":"x1"}"""));
            _ = await pending;
        }
        catch (WorkflowStepFailedException ex)
        {
            caught = ex;
        }

        caught.ShouldNotBeNull();
        caught!.StepId.ShouldBe("receive");
    }

    private static MethodInfo CompileReceiveWithCriteria(out string source)
    {
        var descriptor = new AsyncApiChannelDescriptor(
            "measurements",
            OperationAction.Receive,
            "onMeasured",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("measured", "Corvus.Text.Json.JsonElement", null, null, null)]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveWithCriteriaDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        Assembly assembly = CompileInMemory(source);
        return assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;
    }
}