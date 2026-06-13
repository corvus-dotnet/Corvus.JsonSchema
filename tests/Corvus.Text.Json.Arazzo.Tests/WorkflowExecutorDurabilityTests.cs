// <copyright file="WorkflowExecutorDurabilityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.AsyncApi.Testing;
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
                    [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
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
                [transport, workspace, resumed.Inputs, resumed, default(CancellationToken), null])!;
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
            [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
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
    public async Task Management_client_resumes_a_faulted_run_to_completion()
    {
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableMgmtWorkflow", durable: true);
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableMgmtWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-mgmt-1";

        // ── Run 1: the second step fails (500) → the run faults. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}""")))
        using (var run = WorkflowRun.CreateNew(store, runId, "adoptDurable", inputsDocument.RootElement))
        {
            var transport = new MockApiTransport();
            transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
            transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
            (await pending).IsFaulted.ShouldBeTrue();
        }

        // The operator's resumer re-enters the executor with a healthy dependency (200).
        async ValueTask<WorkflowRunResultKind> Resume(WorkflowRun run, CancellationToken ct)
        {
            using var ws = JsonWorkspace.Create();
            var healthy = new MockApiTransport();
            healthy.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(null, [healthy, ws, run.Inputs, run, ct, null])!;
            return (await pending).Kind;
        }

        var client = new WorkflowManagementClient(store, "ops", Resume);

        // Query: the faulted run is visible via get and the status filter.
        (await client.GetAsync(runId, AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
        (await client.ListAsync(new WorkflowQuery(WorkflowRunStatus.Faulted), AccessContext.System, default))
            .Runs.Select(r => r.Id.Value).ShouldContain(runId.Value);

        // Resume: retry the faulted step → the run completes.
        (await client.ResumeAsync(runId, ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();
        (await client.GetAsync(runId, AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);
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
                [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
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
                [transport, workspace, resumed.Inputs, resumed, default(CancellationToken), null])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsCompleted.ShouldBeTrue();
            result.Outputs.GetProperty("name"u8).GetString().ShouldBe("Charged");
        }
    }

    private const string DurableReceiveDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listenDurable",
              "steps": [
                { "stepId": "receive", "channelPath": "measurements", "action": "receive" }
              ],
              "outputs": { "lumens": "$steps.receive.outputs.lumens" }
            }
          ]
        }
        """;

    private const string DurableReceiveHeaderDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listenDurableHeader",
              "steps": [
                {
                  "stepId": "receive",
                  "channelPath": "measurements",
                  "action": "receive",
                  "outputs": { "trace": "$message.header.x-trace-id", "lumens": "$message.payload#/lumens" }
                }
              ],
              "outputs": { "trace": "$steps.receive.outputs.trace" }
            }
          ]
        }
        """;

    private const string DurableReceiveParameterisedDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "listenDurableParam",
              "steps": [
                { "stepId": "receive", "channelPath": "measurements/{sensorId}", "action": "receive", "parameters": [ { "name": "sensorId", "value": "$inputs.sensorId" } ] }
              ],
              "outputs": { "lumens": "$steps.receive.outputs.lumens" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Durable_executor_suspends_on_a_parameterised_receive_using_the_resolved_channel()
    {
        // A parameterised receive now suspends durably on its RESOLVED address (e.g. "measurements/s1"),
        // so a worker that observes a message on that concrete channel can wake exactly this run.
        var descriptor = new AsyncApiChannelDescriptor(
            "measurements/{sensorId}",
            OperationAction.Receive,
            "onMeasured",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: ["sensorId"],
            Messages: [new AsyncApiChannelMessageDescriptor("measured", "Corvus.Text.Json.JsonElement", null, null, null)]);

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);

        string source;
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(DurableReceiveParameterisedDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenDurableParamWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement", null, true));
        }

        source.ShouldContain("run.SuspendForMessageAsync(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenDurableParamWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();

        using var workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"sensorId":"s1"}"""));
        using var run = WorkflowRun.CreateNew(store, "listen-param-1", "listenDurableParam", inputsDocument.RootElement);

        var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
            null,
            [apiTransport, messageTransport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
        WorkflowRunResult<JsonElement> result = await pending;

        result.IsSuspended.ShouldBeTrue();
        result.Wait.Kind.ShouldBe(WorkflowWaitKind.Message);
        result.Wait.Channel.ShouldBe("measurements/s1");
    }

    [TestMethod]
    public void Durable_receive_that_projects_a_message_header_declares_headers_on_the_delivered_path()
    {
        // A durable receive whose outputs read $message.header must still expose the headers local on the
        // resume-with-delivered-message path (where the worker hands in only the payload).
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(DurableReceiveHeaderDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenDurableHeaderWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement", null, true));
        }

        source.ShouldContain("JsonElement messageHeaders = default;");
        Assembly assembly = CompileInMemory(source);
        assembly.GetType("GeneratedWorkflows.ListenDurableHeaderWorkflow").ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Durable_executor_suspends_on_a_receive_then_resumes_with_the_delivered_message()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(DurableReceiveDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "ListenDurableWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement", null, true));
        }

        source.ShouldContain("run.TryTakeDeliveredMessage(out JsonElement");
        source.ShouldContain("run.SuspendForMessageAsync(");

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.ListenDurableWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "listen-1";
        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();

        // ── Run 1: no message available → the run suspends on a message wait and returns. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}")))
        using (var run = WorkflowRun.CreateNew(store, runId, "listenDurable", inputsDocument.RootElement))
        {
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [apiTransport, messageTransport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsSuspended.ShouldBeTrue();
            result.Wait.Kind.ShouldBe(WorkflowWaitKind.Message);
            result.Wait.Channel.ShouldBe("measurements");
        }

        // ── Run 2: a worker hands the awaited message to the resumed run, which completes immediately. ──
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> payloadDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"lumens":150}""")))
        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, runId))
        {
            resumed.ShouldNotBeNull();
            resumed.Status.ShouldBe(WorkflowRunStatus.Suspended);
            resumed.DeliverMessage(payloadDocument.RootElement);

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null,
                [apiTransport, messageTransport, workspace, resumed.Inputs, resumed, default(CancellationToken), null])!;
            WorkflowRunResult<JsonElement> result = await pending;

            result.IsCompleted.ShouldBeTrue();
            result.Outputs.GetProperty("lumens"u8).GetInt32().ShouldBe(150);

            // The message was consumed from the delivery handoff, not by subscribing to the transport.
            messageTransport.PublishedMessages.Count.ShouldBe(0);
        }
    }

    [TestMethod]
    public async Task Worker_resumes_a_due_timer_run_to_completion()
    {
        string source = EmitGetPetExecutor(DurableRetryDocument, "WorkerTimerWorkflow", durable: true);
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.WorkerTimerWorkflow")!.GetMethod("ExecuteAsync")!;

        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId runId = "worker-timer-1";

        // Run 1 fails and suspends on a 60s durable timer.
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"firstId":"1"}""")))
        using (var run = WorkflowRun.CreateNew(store, runId, "adoptDurableRetry", inputsDocument.RootElement, time))
        {
            var failing = new MockApiTransport();
            failing.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null, [failing, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
            (await pending).IsSuspended.ShouldBeTrue();
        }

        // Before the timer is due, the worker resumes nothing.
        var succeeding = new MockApiTransport();
        succeeding.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Charged"}""");

        async ValueTask<WorkflowRunResultKind> Resume(WorkflowRun run, CancellationToken ct)
        {
            using var ws = JsonWorkspace.Create();
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(null, [succeeding, ws, run.Inputs, run, ct, null])!;
            return (await pending).Kind;
        }

        var worker = new WorkflowWorker(store, "worker-1", time);
        (await worker.ResumeDueTimersAsync(Resume, default)).ShouldBe(0);

        // Once the timer is due, the worker resumes the run and it completes.
        time.Advance(TimeSpan.FromSeconds(120));
        (await worker.ResumeDueTimersAsync(Resume, default)).ShouldBe(1);

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(store, runId, time);
        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Worker_delivers_a_message_and_resumes_the_awaiting_run()
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
        using (var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(DurableReceiveDocument)))
        {
            ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
            source = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("GeneratedWorkflows", "WorkerMessageWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement", null, true));
        }

        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.WorkerMessageWorkflow")!.GetMethod("ExecuteAsync")!;

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "worker-msg-1";
        var apiTransport = new MockApiTransport();
        await using var messageTransport = new InMemoryMessageTransport();

        // Run 1 suspends awaiting a message on "measurements".
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}")))
        using (var run = WorkflowRun.CreateNew(store, runId, "listenDurable", inputsDocument.RootElement))
        {
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null, [apiTransport, messageTransport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
            (await pending).IsSuspended.ShouldBeTrue();
        }

        async ValueTask<WorkflowRunResultKind> Resume(WorkflowRun run, CancellationToken ct)
        {
            using var ws = JsonWorkspace.Create();
            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(null, [apiTransport, messageTransport, ws, run.Inputs, run, ct, null])!;
            return (await pending).Kind;
        }

        var worker = new WorkflowWorker(store, "worker-1");
        using (ParsedJsonDocument<JsonElement> payloadDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"lumens":150}""")))
        {
            int resumed = await worker.DeliverMessageAsync("measurements", null, payloadDocument.RootElement, Resume, default);
            resumed.ShouldBe(1);
        }

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(store, runId);
        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(WorkflowRunStatus.Completed);
        completed.TryGetStepOutputs("receive", out JsonElement received).ShouldBeTrue();
        received.GetProperty("lumens"u8).GetInt32().ShouldBe(150);
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
            [transport, workspace, inputsDocument.RootElement, null, default(CancellationToken), null])!;
        WorkflowRunResult<JsonElement> result = await pending;

        result.IsCompleted.ShouldBeTrue();
        result.Outputs.GetProperty("first"u8).GetString().ShouldBe("Solo");
        result.Outputs.GetProperty("second"u8).GetString().ShouldBe("Solo");
        transport.Requests.Count.ShouldBe(2);
    }
}