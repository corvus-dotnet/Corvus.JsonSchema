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

    private const string ChannelRequestReplyDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "ask",
              "steps": [
                {
                  "stepId": "query",
                  "channelPath": "queries",
                  "action": "send",
                  "requestBody": { "payload": "$inputs.q" },
                  "outputs": { "answer": "$message.payload#/answer" }
                }
              ],
              "outputs": { "answer": "$steps.query.outputs.answer" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_performs_request_reply_and_captures_the_reply()
    {
        // A request/reply descriptor: a send operation that declares a reply, so the producer exposes a
        // SendAndReceive method returning the reply payload.
        var descriptor = new AsyncApiChannelDescriptor(
            "queries",
            OperationAction.Send,
            "query",
            "Acme.Rpc.QueryProducer",
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("query", "Corvus.Text.Json.JsonElement", null, null, "PublishQueryAsync", "SendAndReceiveQueryAsync")],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelRequestReplyDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "AskWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The step calls the producer's request/reply method and projects the reply.
        source.ShouldContain("new Acme.Rpc.QueryProducer(messageTransport)");
        source.ShouldContain(".SendAndReceiveQueryAsync(");
        source.ShouldContain("JsonElement queryReplyPayload = JsonElement.From(queryReply)");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AskWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"q":{"text":"meaning"}}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        // The reply's projected field flowed to the workflow output.
        outputs.TryGetProperty("answer"u8, out JsonElement answer).ShouldBeTrue();
        answer.GetInt32().ShouldBe(42);
    }

    private const string ChannelReceiveReplyDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "respond",
              "steps": [
                {
                  "stepId": "serve",
                  "channelPath": "requests",
                  "action": "receive",
                  "requestBody": { "payload": "$message.payload" },
                  "successCriteria": [ { "condition": "$message.payload#/n == 21" } ],
                  "outputs": { "n": "$message.payload#/n" }
                }
              ],
              "outputs": { "served": "$steps.serve.outputs.n" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_receives_a_request_and_replies_one_shot()
    {
        // A request/reply receive descriptor: a receive operation that declares a reply, so the step is a
        // one-shot responder — it receives one request, replies with its requestBody (here echoing the
        // request payload), gates on the request, projects an output, and unsubscribes.
        var descriptor = new AsyncApiChannelDescriptor(
            "requests",
            OperationAction.Receive,
            "onRequest",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("request", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveReplyDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "RespondWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The responder step subscribes via the one-shot reply wrapper and returns the reply payload.
        source.ShouldContain("messageTransport.ReceiveOneAndReplyAsync<Corvus.Text.Json.JsonElement, Corvus.Text.Json.JsonElement>(");
        source.ShouldContain("return new ValueTask<Corvus.Text.Json.JsonElement>(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RespondWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        // Start the responder: it subscribes synchronously, then awaits one request.
        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        // Send a request; the in-process responder replies with the echoed request payload (the whole round
        // trip completes synchronously on this thread, keeping the thread-affine workspace on its owner).
        using var requestDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"n":21}"""));
        (JsonElement reply, JsonElement _) = await messageTransport.RequestAsync<JsonElement, JsonElement>(
            "requests"u8.ToArray(),
            "replies"u8.ToArray(),
            requestDocument.RootElement,
            "corr-serve"u8.ToArray());

        JsonElement outputs = await pending;

        // The reply echoes the request payload; the request's field flowed to the workflow output.
        reply.TryGetProperty("n"u8, out JsonElement replyN).ShouldBeTrue();
        replyN.GetInt32().ShouldBe(21);
        outputs.TryGetProperty("served"u8, out JsonElement served).ShouldBeTrue();
        served.GetInt32().ShouldBe(21);
    }

    private const string ChannelReceiveReplyControlFlowDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "respond",
              "steps": [
                {
                  "stepId": "serve",
                  "channelPath": "requests",
                  "action": "receive",
                  "requestBody": { "payload": "$message.payload#/echo" },
                  "onSuccess": [ { "name": "stop", "type": "end" } ],
                  "outputs": { "v": "$message.payload#/echo" }
                }
              ],
              "outputs": { "v": "$steps.serve.outputs.v" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_responder_runs_in_the_control_flow_loop()
    {
        // A responder step with an onSuccess action is promoted into the control-flow loop; the reply is a
        // pointer projection of the request payload (exercising the navigated-reply path).
        var descriptor = new AsyncApiChannelDescriptor(
            "requests",
            OperationAction.Receive,
            "onRequest",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("request", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveReplyControlFlowDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "RespondWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The responder is promoted into the control-flow loop and still replies via the one-shot wrapper.
        source.ShouldContain("while (true)");
        source.ShouldContain("messageTransport.ReceiveOneAndReplyAsync<Corvus.Text.Json.JsonElement, Corvus.Text.Json.JsonElement>(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RespondWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        using var requestDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"echo":{"k":7}}"""));
        (JsonElement reply, JsonElement _) = await messageTransport.RequestAsync<JsonElement, JsonElement>(
            "requests"u8.ToArray(),
            "replies"u8.ToArray(),
            requestDocument.RootElement,
            "corr-cf"u8.ToArray());

        JsonElement outputs = await pending;

        // The reply is the nested object the pointer addressed; the same value flowed to the output.
        reply.TryGetProperty("k"u8, out JsonElement replyK).ShouldBeTrue();
        replyK.GetInt32().ShouldBe(7);
        outputs.TryGetProperty("v"u8, out JsonElement v).ShouldBeTrue();
        v.TryGetProperty("k"u8, out JsonElement vk).ShouldBeTrue();
        vk.GetInt32().ShouldBe(7);
    }

    private const string ChannelReceiveConstantReplyDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "respond",
              "steps": [
                {
                  "stepId": "serve",
                  "channelPath": "requests",
                  "action": "receive",
                  "requestBody": { "payload": { "status": "accepted", "code": 202 } },
                  "outputs": { "n": "$message.payload#/n" }
                }
              ],
              "outputs": { "served": "$steps.serve.outputs.n" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_responder_replies_with_a_constant_object()
    {
        // A responder whose reply is a constant object (no embedded expressions): it is parsed once into a
        // standalone document and returned regardless of the request, while outputs still project the request.
        var descriptor = new AsyncApiChannelDescriptor(
            "requests",
            OperationAction.Receive,
            "onRequest",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("request", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveConstantReplyDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "RespondWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The constant reply is baked into a static document and referenced.
        source.ShouldContain("ParsedJsonDocument<JsonElement>");
        source.ShouldContain("messageTransport.ReceiveOneAndReplyAsync<Corvus.Text.Json.JsonElement, Corvus.Text.Json.JsonElement>(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RespondWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        using var requestDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"n":99}"""));
        (JsonElement reply, JsonElement _) = await messageTransport.RequestAsync<JsonElement, JsonElement>(
            "requests"u8.ToArray(),
            "replies"u8.ToArray(),
            requestDocument.RootElement,
            "corr-const"u8.ToArray());

        JsonElement outputs = await pending;

        // The reply is the constant object; the output projected the request field.
        reply.TryGetProperty("status"u8, out JsonElement status).ShouldBeTrue();
        status.GetString().ShouldBe("accepted");
        reply.TryGetProperty("code"u8, out JsonElement code).ShouldBeTrue();
        code.GetInt32().ShouldBe(202);
        outputs.TryGetProperty("served"u8, out JsonElement served).ShouldBeTrue();
        served.GetInt32().ShouldBe(99);
    }

    [TestMethod]
    public void Responder_step_without_requestBody_is_rejected()
    {
        NotSupportedException ex = EmitResponder("""{ "stepId": "serve", "channelPath": "requests", "action": "receive" }""");
        ex.Message.ShouldContain("requestBody");
    }

    [TestMethod]
    public void Responder_step_with_interpolated_reply_is_rejected()
    {
        NotSupportedException ex = EmitResponder("""{ "stepId": "serve", "channelPath": "requests", "action": "receive", "requestBody": { "payload": "echo-{$message.payload#/n}" } }""");
        ex.Message.ShouldContain("interpolated reply");
    }

    [TestMethod]
    public void Responder_step_with_unresolvable_reply_expression_is_rejected()
    {
        NotSupportedException ex = EmitResponder("""{ "stepId": "serve", "channelPath": "requests", "action": "receive", "requestBody": { "payload": "$url" } }""");
        ex.Message.ShouldContain("cannot be resolved");
    }

    // Emits a single-step responder workflow whose only step is the supplied JSON, returning the
    // NotSupportedException the emitter throws (failing the test if it does not throw one).
    private static NotSupportedException EmitResponder(string stepJson)
    {
        string document = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
              "workflows": [ { "workflowId": "respond", "steps": [ {{stepJson}} ] } ]
            }
            """;

        var descriptor = new AsyncApiChannelDescriptor(
            "requests",
            OperationAction.Receive,
            "onRequest",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("request", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        return Should.Throw<NotSupportedException>(() => WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("GeneratedWorkflows", "RespondWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement")));
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

    private const string ChannelReceiveWithHeaderOutputsDocument = """
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
                  "outputs": { "trace": "$message.header.x-trace-id", "temp": "$message.payload#/temp" }
                }
              ],
              "outputs": { "trace": "$steps.receive.outputs.trace", "temp": "$steps.receive.outputs.temp" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_projects_message_header_outputs()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveWithHeaderOutputsDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The header value is read off the handler's headers parameter.
        source.ShouldContain("messageHeaders.TryGetProperty(\"x-trace-id\"u8");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>(
            "measurements",
            Encoding.UTF8.GetBytes("""{"temp":21}"""),
            Encoding.UTF8.GetBytes("""{"x-trace-id":"abc-123"}"""));
        JsonElement outputs = await pending;

        outputs.TryGetProperty("trace"u8, out JsonElement trace).ShouldBeTrue();
        trace.GetString().ShouldBe("abc-123");
        outputs.TryGetProperty("temp"u8, out JsonElement temp).ShouldBeTrue();
        temp.GetInt32().ShouldBe(21);
    }

    private const string ChannelReceiveWithHeaderCriteriaDocument = """
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
                  "successCriteria": [ { "condition": "$message.header.x-status == 'ok'" } ],
                  "outputs": { "temp": "$message.payload#/temp" }
                }
              ],
              "outputs": { "temp": "$steps.receive.outputs.temp" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_gates_a_received_message_on_header_criteria()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveWithHeaderCriteriaDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The header criterion is inlined against the handler's headers parameter (no context).
        source.ShouldContain("messageHeaders.TryGetProperty(\"x-status\"u8");
        source.ShouldNotContain("WorkflowExecutionContext");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        // Pass: the header satisfies the criterion.
        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>(
            "measurements",
            Encoding.UTF8.GetBytes("""{"temp":21}"""),
            Encoding.UTF8.GetBytes("""{"x-status":"ok"}"""));
        JsonElement outputs = await pending;
        outputs.TryGetProperty("temp"u8, out JsonElement temp).ShouldBeTrue();
        temp.GetInt32().ShouldBe(21);

        // Fail: a header that misses the criterion fails the step.
        await using var messageTransport2 = new InMemoryMessageTransport();
        using var workspace2 = JsonWorkspace.Create();
        var pending2 = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport2, workspace2, inputsDocument.RootElement, default(CancellationToken)])!;
        WorkflowStepFailedException? caught = null;
        try
        {
            await messageTransport2.DeliverAsync<JsonElement>(
                "measurements",
                Encoding.UTF8.GetBytes("""{"temp":21}"""),
                Encoding.UTF8.GetBytes("""{"x-status":"bad"}"""));
            _ = await pending2;
        }
        catch (WorkflowStepFailedException ex)
        {
            caught = ex;
        }

        caught.ShouldNotBeNull();
    }

    private const string ChannelReceiveRetryDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listen",
              "steps": [
                {
                  "stepId": "wait",
                  "channelPath": "measurements",
                  "action": "receive",
                  "successCriteria": [ { "condition": "$message.payload#/status == 'ready'" } ],
                  "onFailure": [ { "name": "again", "type": "retry", "retryAfter": 0, "retryLimit": 5 } ],
                  "outputs": { "id": "$message.payload#/id" }
                }
              ],
              "outputs": { "id": "$steps.wait.outputs.id" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_retries_a_receive_step_until_a_message_matches()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveRetryDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The receive step is promoted into the control-flow loop (onFailure retry).
        source.ShouldContain("while (true)");
        source.ShouldContain("messageTransport.ReceiveOneAsync<Corvus.Text.Json.JsonElement>(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;

        // First message misses the criterion → the step retries (re-subscribes); the second matches.
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"status":"pending","id":"R1"}"""));
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"status":"ready","id":"R2"}"""));
        JsonElement outputs = await pending;

        outputs.TryGetProperty("id"u8, out JsonElement id).ShouldBeTrue();
        id.GetString().ShouldBe("R2");
    }

    private const string ChannelReceiveDynamicCriteriaDocument = """
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
                  "successCriteria": [ { "context": "$message.payload#/name", "type": "regex", "condition": "^{$inputs.prefix}" } ],
                  "outputs": { "name": "$message.payload#/name" }
                }
              ],
              "outputs": { "name": "$steps.receive.outputs.name" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_gates_a_receive_on_a_dynamic_pattern_criterion()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveDynamicCriteriaDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The embedded {$inputs.prefix} makes the pattern dynamic → a CompiledCriterion evaluated against
        // a context fed with the received message payload.
        source.ShouldContain("CompiledCriterion");
        source.ShouldContain("context.SetMessagePayload(receiveMessagePayload)");
        source.ShouldContain("new WorkflowExecutionContext()");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"prefix":"id"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken)])!;
        await messageTransport.DeliverAsync<JsonElement>("measurements", Encoding.UTF8.GetBytes("""{"name":"id-42"}"""));
        JsonElement outputs = await pending;

        // The dynamic pattern "^id" (built from $inputs.prefix) matched the message's name.
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("id-42");
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