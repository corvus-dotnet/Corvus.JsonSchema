// <copyright file="WorkflowExecutorDurabilityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof of the durable (checkpoint &amp; resume) executor shape (plan §9.3): a generated durable
/// executor checkpoints after each step against an <see cref="InMemoryWorkflowStateStore"/>; when a run is
/// interrupted partway, a worker re-enters from the last checkpoint and finishes <em>without re-running</em>
/// the already-completed steps, with their outputs restored into the final workflow outputs.
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

    [TestMethod]
    public async Task Durable_executor_checkpoints_after_each_step()
    {
        // A durable executor threads an IWorkflowRun and writes a checkpoint after every step; with a null
        // run it must behave exactly like the non-durable form.
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableWorkflow", durable: true);
        source.ShouldContain("IWorkflowRun? run = null");
        source.ShouldContain("run.CheckpointAsync(__state, cancellationToken)");
        source.ShouldContain("run.CompleteAsync(workflowOutputsElement, cancellationToken)");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-1";

        // ── Run 1: completes the first step, then is interrupted at the second (a 500 fails its success
        // criteria, so the run stops). The checkpoint written after the first step survives. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}""")))
        using (var run = WorkflowRun.CreateNew(store, runId, "adoptDurable", inputsDocument.RootElement))
        {
            var transport = new MockApiTransport();
            transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
            transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

            WorkflowStepFailedException? failure = null;
            try
            {
                var pending = (ValueTask<JsonElement>)execute.Invoke(
                    null,
                    [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken)])!;
                await pending;
            }
            catch (WorkflowStepFailedException ex)
            {
                failure = ex;
            }

            failure.ShouldNotBeNull();
            transport.Requests.Count.ShouldBe(2);
        }

        // The checkpoint persisted at the first step's completion: cursor advanced past it, its outputs staged.
        using (WorkflowRun? afterCrash = await WorkflowRun.ResumeAsync(store, runId))
        {
            afterCrash.ShouldNotBeNull();
            afterCrash.Cursor.ShouldBe(1);
            afterCrash.TryGetStepOutputs("first", out JsonElement first).ShouldBeTrue();
            first.GetProperty("firstName"u8).GetString().ShouldBe("Alpha");
            afterCrash.TryGetStepOutputs("second", out _).ShouldBeFalse();
        }

        // ── Run 2: a worker resumes from the checkpoint and finishes the second step. ──
        using (var workspace = JsonWorkspace.Create())
        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, runId))
        {
            resumed.ShouldNotBeNull();

            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Beta"}""");

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, resumed.Inputs, resumed, default(CancellationToken)])!;
            JsonElement outputs = await pending;

            // The restored first-step output flows into the final outputs; the second step ran fresh.
            outputs.GetProperty("first"u8).GetString().ShouldBe("Alpha");
            outputs.GetProperty("second"u8).GetString().ShouldBe("Beta");

            // The already-completed first step was NOT re-run: only the second step's call was made.
            transport.Requests.Count.ShouldBe(1);
            transport.Requests[0].Path.ShouldBe("/pets/2");
        }

        // The terminal checkpoint records the run as completed.
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(store, runId);
        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(WorkflowRunStatus.Completed);
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

        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [transport, workspace, inputsDocument.RootElement, null, default(CancellationToken)])!;
        JsonElement outputs = await pending;

        outputs.GetProperty("first"u8).GetString().ShouldBe("Solo");
        outputs.GetProperty("second"u8).GetString().ShouldBe("Solo");
        transport.Requests.Count.ShouldBe(2);
    }
}