// <copyright file="WorkflowExecutorControlFlowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof that a workflow declaring <c>onSuccess</c>/<c>onFailure</c> actions emits a
/// labelled-loop executor that compiles and runs the right control flow: retry, goto, and end.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string RetryDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "retryAdopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [
                    { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 3, "criteria": [ { "condition": "$statusCode == 500" } ] }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string EndOnFailureDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "endAdopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "stop", "type": "end" } ]
                }
              ],
              "outputs": { "echo": "$inputs.petId" }
            }
          ]
        }
        """;

    private const string GotoDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "gotoAdopt",
              "steps": [
                {
                  "stepId": "step1",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onSuccess": [ { "name": "skip", "type": "goto", "stepId": "step3" } ]
                },
                {
                  "stepId": "step2",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ]
                },
                {
                  "stepId": "step3",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.step3.outputs.petName" }
            }
          ]
        }
        """;

    private const string RetryWithDelayDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "retryDelayAdopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [
                    { "name": "retry5xx", "type": "retry", "retryAfter": 5, "retryLimit": 3, "criteria": [ { "condition": "$statusCode == 500" } ] }
                  ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_uses_the_injected_TimeProvider_for_the_retry_delay()
    {
        string source = EmitGetPetExecutor(RetryWithDelayDocument, "RetryDelayWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RetryDelayWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        var timeProvider = new ImmediateRecordingTimeProvider();

        // retryAfter is 5s; because the executor honours the injected TimeProvider, the delay is driven by
        // this provider (which fires timers immediately) rather than the wall clock — so the test completes
        // promptly and the provider records that it created the delay timer.
        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), timeProvider])!;
        JsonElement outputs = await pending;

        transport.Requests.Count.ShouldBe(2);
        outputs.GetProperty("name"u8).GetString().ShouldBe("Fido");
        timeProvider.TimersCreated.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void Emits_a_labelled_loop_with_inlined_action_criteria_and_no_context()
    {
        // The success and the retry action criteria are both bare $statusCode comparisons, so they inline
        // directly: the control-flow step needs no WorkflowExecutionContext at all.
        string source = EmitGetPetExecutor(RetryDocument, "RetryAdoptWorkflow");

        source.ShouldContain("while (true)");
        source.ShouldContain("switch (__state)");
        source.ShouldContain("goto __workflowOutputs;");
        source.ShouldContain("getPetRetryCount");
        source.ShouldContain("await Task.Delay(");
        source.ShouldNotContain("WorkflowExecutionContext");
        source.ShouldNotContain("CompiledAction");
    }

    [TestMethod]
    public async Task Generated_executor_retries_a_failed_step_then_succeeds()
    {
        string source = EmitGetPetExecutor(RetryDocument, "RetryAdoptWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RetryAdoptWorkflow")!.GetMethod("ExecuteAsync")!;

        // First attempt fails (500), the retry succeeds (200).
        var transport = new MockApiTransport();
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        transport.Requests.Count.ShouldBe(2);
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    [TestMethod]
    public async Task Generated_executor_fails_when_retries_are_exhausted()
    {
        const string exhaust = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
              "workflows": [
                {
                  "workflowId": "retryAdopt",
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                      "successCriteria": [ { "condition": "$statusCode == 200" } ],
                      "onFailure": [ { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 1 } ]
                    }
                  ],
                  "outputs": {}
                }
              ]
            }
            """;
        string source = EmitGetPetExecutor(exhaust, "RetryExhaustWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RetryExhaustWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        WorkflowStepFailedException? caught = null;
        try
        {
            var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
            await pending;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is WorkflowStepFailedException failed)
        {
            caught = failed;
        }
        catch (WorkflowStepFailedException failed)
        {
            caught = failed;
        }

        caught.ShouldNotBeNull();
        // retryLimit 1 → one initial attempt plus one retry.
        transport.Requests.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task Generated_executor_ends_gracefully_on_a_failure_end_action()
    {
        string source = EmitGetPetExecutor(EndOnFailureDocument, "EndAdoptWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.EndAdoptWorkflow")!.GetMethod("ExecuteAsync")!;

        // The step fails (500) and its onFailure end action stops the workflow without throwing.
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        transport.Requests.Count.ShouldBe(1);
        // The workflow output reads $inputs, so it resolves even though the step did not succeed.
        outputs.TryGetProperty("echo"u8, out JsonElement echo).ShouldBeTrue();
        echo.GetString().ShouldBe("42");
    }

    [TestMethod]
    public async Task Generated_executor_follows_an_onSuccess_goto_skipping_a_step()
    {
        string source = EmitGetPetExecutor(GotoDocument, "GotoAdoptWorkflow");
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.GotoAdoptWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // step1 succeeds and gotos step3 — step2 is skipped, so only two requests are made.
        transport.Requests.Count.ShouldBe(2);
        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
    }

    /// <summary>A <see cref="TimeProvider"/> that fires every timer immediately and records how many it
    /// created — so a <c>Task.Delay</c> routed through it completes at once and the test can assert it was used.</summary>
    private sealed class ImmediateRecordingTimeProvider : TimeProvider
    {
        private int timersCreated;

        public int TimersCreated => Volatile.Read(ref this.timersCreated);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref this.timersCreated);
            return new ImmediateTimer(callback, state);
        }

        private sealed class ImmediateTimer : ITimer
        {
            public ImmediateTimer(TimerCallback callback, object? state) => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}