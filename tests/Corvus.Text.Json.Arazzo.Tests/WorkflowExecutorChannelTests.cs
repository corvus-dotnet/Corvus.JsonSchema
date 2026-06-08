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
}