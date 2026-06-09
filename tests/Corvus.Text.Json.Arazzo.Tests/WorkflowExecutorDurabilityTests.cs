// <copyright file="WorkflowExecutorDurabilityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof of the durable (checkpoint &amp; resume) executor shape (plan §9.3/§9.4): a generated
/// durable executor returns the tri-state <see cref="WorkflowRunResult{T}"/>, checkpoints after each step
/// against an <see cref="InMemoryWorkflowStateStore"/>, resumes from the last checkpoint after an
/// uncontrolled crash, returns <c>Faulted</c> when a step fails, and <c>Suspended</c> on a durable timer —
/// then completes when re-entered.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string DurableTwoStepDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptDurable",
              "steps": [
                {
                  "stepId": "first",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.firstId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "firstName": "$response.body#/name" }
                },
                {
                  "stepId": "second",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.secondId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "secondName": "$response.body#/name" }
                }
              ],
              "outputs": {
                "first": "$steps.first.outputs.firstName",
                "second": "$steps.second.outputs.secondName"
              }
            }
          ]
        }
        """;

    // A single step that retries with a delay on failure — drives the durable timer-suspension path.
    private const string DurableRetryDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adoptDurableRetry",
              "steps": [
                {
                  "stepId": "charge",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.firstId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "chargeName": "$response.body#/name" },
                  "onFailure": [ { "name": "again", "type": "retry", "retryAfter": 60, "retryLimit": 3 } ]
                }
              ],
              "outputs": { "name": "$steps.charge.outputs.chargeName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Durable_executor_resumes_from_the_last_checkpoint_after_a_crash()
    {
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableWorkflow", durable: true);
        source.ShouldContain("IWorkflowRun? run = null");
        source.ShouldContain("ValueTask<WorkflowRunResult<Corvus.Text.Json.JsonElement>>");
        source.ShouldContain("run.CheckpointAsync(__state, cancellationToken)");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-1";

        // ── Run 1: completes the first step, then crashes (raw exception) at the second. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}""")))
        using (var run = WorkflowRun.CreateNew(store, runId, "adoptDurable", inputsDocument.RootElement))
        {
            var inner = new MockApiTransport();
            inner.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
            var transport = new CrashingApiTransport(inner, crashOnCall: 2);

            InvalidOperationException? crash = null;
            try
            {
                var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                    null,
                    [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken)])!;
                await pending;
            }
            catch (InvalidOperationException ex)
            {
                crash = ex;
            }

            crash.ShouldNotBeNull();
        }

        // The checkpoint after the first step survived the crash: cursor advanced, its outputs staged.
        using (WorkflowRun? afterCrash = await WorkflowRun.ResumeAsync(store, runId))
        {
            afterCrash.ShouldNotBeNull();
            afterCrash.Cursor.ShouldBe(1);
            afterCrash.TryGetStepOutputs("first", out JsonElement first).ShouldBeTrue();
            first.GetProperty("firstName"u8).GetString().ShouldBe("Alpha");
            afterCrash.TryGetStepOutputs("second", out _).ShouldBeFalse();
        }

        // ── Run 2: a worker resumes and finishes the second step without re-running the first. ──
        using (var workspace = JsonWorkspace.Create())
        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, runId))
        {
            resumed.ShouldNotBeNull();

            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Beta"}""");

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [transport, workspace, resumed.Inputs, resumed, default(CancellationToken)])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsCompleted.ShouldBeTrue();
            result.Outputs.GetProperty("first"u8).GetString().ShouldBe("Alpha");
            result.Outputs.GetProperty("second"u8).GetString().ShouldBe("Beta");

            // The already-completed first step was NOT re-run: only the second step's call was made.
            transport.Requests.Count.ShouldBe(1);
            transport.Requests[0].Path.ShouldBe("/pets/2");
        }

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(store, runId);
        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Durable_executor_returns_faulted_when_a_step_fails()
    {
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableFaultWorkflow", durable: true);
        source.ShouldContain("run.FaultAsync(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableFaultWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-fault-1";

        using var workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}"""));
        using var run = WorkflowRun.CreateNew(store, runId, "adoptDurable", inputsDocument.RootElement);

        var transport = new MockApiTransport();
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

        var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken)])!;
        WorkflowRunResult<JsonElement> result = await pending;

        result.IsFaulted.ShouldBeTrue();
        result.Fault.StepId.ShouldBe("second");

        // The fault was persisted; the run is recoverable, not lost.
        using WorkflowRun? faulted = await WorkflowRun.ResumeAsync(store, runId);
        faulted.ShouldNotBeNull();
        faulted.Status.ShouldBe(WorkflowRunStatus.Faulted);
        faulted.Fault!.Value.StepId.ShouldBe("second");
    }

    [TestMethod]
    public async Task Durable_executor_suspends_on_a_timer_then_resumes_to_completion()
    {
        string source = EmitGetPetExecutor(DurableRetryDocument, "AdoptDurableRetryWorkflow", durable: true);
        source.ShouldContain("run.SuspendForTimerAsync(__state, TimeSpan.FromSeconds(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableRetryWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-retry-1";

        // ── Run 1: the step fails, the retry has a delay → the run suspends on a durable timer. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1"}""")))
        using (var run = WorkflowRun.CreateNew(store, runId, "adoptDurableRetry", inputsDocument.RootElement))
        {
            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken)])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsSuspended.ShouldBeTrue();
            result.Wait.Kind.ShouldBe(WorkflowWaitKind.Timer);
        }

        // ── Run 2: the worker resumes when the timer is due; the step now succeeds. ──
        using (var workspace = JsonWorkspace.Create())
        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, runId))
        {
            resumed.ShouldNotBeNull();
            resumed.Status.ShouldBe(WorkflowRunStatus.Suspended);
            resumed.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Timer);

            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Charged"}""");

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [transport, workspace, resumed.Inputs, resumed, default(CancellationToken)])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsCompleted.ShouldBeTrue();
            result.Outputs.GetProperty("name"u8).GetString().ShouldBe("Charged");
        }
    }

    [TestMethod]
    public async Task Durable_executor_with_a_null_run_behaves_like_the_non_durable_form()
    {
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableNullRunWorkflow", durable: true);

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableNullRunWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Solo"}""");

        using var workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}"""));

        var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, null, default(CancellationToken)])!;
        WorkflowRunResult<JsonElement> result = await pending;

        result.IsCompleted.ShouldBeTrue();
        result.Outputs.GetProperty("first"u8).GetString().ShouldBe("Solo");
        result.Outputs.GetProperty("second"u8).GetString().ShouldBe("Solo");
        transport.Requests.Count.ShouldBe(2);
    }
}