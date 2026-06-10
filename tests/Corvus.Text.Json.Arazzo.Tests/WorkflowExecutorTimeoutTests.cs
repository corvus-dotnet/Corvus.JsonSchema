// <copyright file="WorkflowExecutorTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.AsyncApi.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end coverage of a step's <c>timeout</c> (Arazzo 1.1): a step that does not complete within
/// its budget is cancelled and treated as a failure, routing to the step's <c>onFailure</c> dispatch
/// (or the default workflow failure when there is none). A timeout forces the control-flow loop.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string TimeoutDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "timeout": 30,
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string TimeoutEndDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "timeout": 30,
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "bail", "type": "end" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_times_out_a_slow_step_and_fails()
    {
        string source = EmitGetPetExecutor(TimeoutDocument, "AdoptTimeoutWorkflow");

        // A timeout forces the control-flow loop and emits a linked, time-limited CTS for the call.
        source.ShouldContain("CancelAfter(30)");
        source.ShouldContain("CreateLinkedTokenSource(cancellationToken)");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptTimeoutWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        transport.SetResponseDelay(TimeSpan.FromSeconds(2));

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;

        WorkflowStepFailedException? caught = null;
        try
        {
            _ = await pending;
        }
        catch (WorkflowStepFailedException ex)
        {
            caught = ex;
        }

        // The slow step exceeded its 30ms budget, so it failed (no onFailure action ⇒ default fail).
        caught.ShouldNotBeNull();
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.completed").ShouldBe(0);
    }

    [TestMethod]
    public async Task Generated_executor_with_a_timeout_completes_when_the_step_is_fast()
    {
        string source = EmitGetPetExecutor(TimeoutDocument, "AdoptTimeoutFastWorkflow");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptTimeoutFastWorkflow")!.GetMethod("ExecuteAsync")!;

        // No delay: the step completes well inside its budget, so the timeout never fires.
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
        recorded.Sum("corvus.arazzo.workflows.completed").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(0);
    }

    [TestMethod]
    public async Task Generated_executor_routes_a_step_timeout_to_on_failure()
    {
        string source = EmitGetPetExecutor(TimeoutEndDocument, "AdoptTimeoutEndWorkflow");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptTimeoutEndWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        transport.SetResponseDelay(TimeSpan.FromSeconds(2));

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        _ = await pending;

        // The timeout routed to onFailure's unconditional `end`, so the workflow completed gracefully
        // instead of faulting — the step never produced a response, but `end` ended it cleanly.
        recorded.Sum("corvus.arazzo.workflows.completed").ShouldBe(1);
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(0);
    }

    private const string ChannelReceiveTimeoutDocument = """
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
                  "timeout": 30
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_times_out_a_receive_step_when_no_message_arrives()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ChannelReceiveTimeoutDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenTimeoutWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
        }

        // The receive awaits on the timed token (the linked CTS), not the bare cancellation token.
        source.ShouldContain("CancelAfter(30)");
        source.ShouldContain("receiveCts.Token).ConfigureAwait(false)");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenTimeoutWorkflow")!.GetMethod("ExecuteAsync")!;

        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();

        using var recorded = new RecordedTelemetry();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        // No message is ever delivered, so the receive exceeds its 30ms budget and fails the step.
        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;

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
        recorded.Sum("corvus.arazzo.workflows.faulted").ShouldBe(1);
    }

    private const string ParentSubWorkflowTimeoutDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                {
                  "stepId": "callChild",
                  "workflowId": "child",
                  "timeout": 30,
                  "parameters": [ { "name": "petId", "value": "$inputs.petId" } ]
                }
              ],
              "outputs": { "name": "$steps.callChild.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public void Emits_a_timeout_for_a_sub_workflow_step()
    {
        string source = EmitGetPetExecutor(ParentSubWorkflowTimeoutDocument, "ParentTimeoutWorkflow");

        // The sub-workflow call runs on the timed token, and a timeout (not the caller's cancellation)
        // is caught and treated as a step failure.
        source.ShouldContain("CancelAfter(30)");
        source.ShouldContain("callChildCts.Token).ConfigureAwait(false)");
        source.ShouldContain("catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)");
    }
}
