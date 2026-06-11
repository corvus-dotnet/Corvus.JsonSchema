// <copyright file="WorkflowExecutorTelemetryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
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
/// Proves the run-wide telemetry correlation id (the W3C trace id) is threaded through the whole flow: a
/// step's outbound request span shares the run's trace, so the originating workflow can be found from the
/// telemetry a downstream web request emits (the trace→run pivot via <c>?correlationId=</c>) — and that this
/// survives a durable resume, where the trace is rehydrated from the checkpoint after the original context
/// is gone.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    [TestMethod]
    public async Task Telemetry_correlation_id_links_a_step_request_to_its_run_across_resume()
    {
        string source = EmitGetPetExecutor(DurableTwoStepDocument, "AdoptDurableTelemetryWorkflow", durable: true);
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.AdoptDurableTelemetryWorkflow")!.GetMethod("ExecuteAsync")!;

        // Capture every completed span across all sources (the Arazzo runtime + the stand-in HTTP client).
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => spans.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId runId = "adopt-telemetry-1";
        using var workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputsDocument = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"firstId":"1","secondId":"2"}"""));

        string correlationId;
        using var incoming = new ActivitySource("Test.Incoming");

        // ── Run 1, under an ambient "incoming request" span: the run's correlation id is captured from it,
        //    and the second step returns 500 so the run faults. ──
        using (Activity request = incoming.StartActivity("incoming.request")!)
        {
            correlationId = request.TraceId.ToString();
            using var run = WorkflowRun.CreateNew(store, runId, "adoptDurable", inputsDocument.RootElement);
            run.CorrelationId.ShouldBe(correlationId, "the run adopts the ambient trace id as its correlation id");

            var faultingInner = new MockApiTransport();
            faultingInner.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Alpha"}""");
            faultingInner.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
            await using var faulting = new TracingApiTransport(faultingInner);

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null, [faulting, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
            (await pending).IsFaulted.ShouldBeTrue();
        }

        // ── Resume with NO ambient context: the executor must rehydrate the trace from the stored
        //    correlation id, so the resumed step's request still rides the original trace. ──
        Activity.Current.ShouldBeNull();
        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, runId))
        {
            resumed.ShouldNotBeNull();
            resumed.CorrelationId.ShouldBe(correlationId);

            var healthyInner = new MockApiTransport();
            healthyInner.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Beta"}""");
            await using var healthy = new TracingApiTransport(healthyInner);

            var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(
                null, [healthy, workspace, inputsDocument.RootElement, resumed, default(CancellationToken), null])!;
            (await pending).IsFaulted.ShouldBeFalse("the retried step succeeds on resume");
        }

        // (1) Every outbound step request — initial AND resumed — shares the run's correlation id, so a
        //     downstream service's span carries the id the control plane filters on (?correlationId=).
        List<Activity> requests = spans.Where(a => a.OperationName == TracingApiTransport.SpanName).ToList();
        requests.Count.ShouldBeGreaterThanOrEqualTo(2, "at least the faulting attempt and the resumed retry");
        requests.ShouldAllBe(a => a.TraceId.ToString() == correlationId);

        // (4) Specifically, the request made by the RESUMED run (after the ambient context was gone) is on
        //     the original trace — proving the checkpoint rehydration, not just process-local propagation.
        // (3) The workflow root spans are in the same trace.
        spans.Where(a => a.OperationName == "workflow.adoptDurable")
            .ShouldAllBe(a => a.TraceId.ToString() == correlationId);

        // (2) The checkpoint spans carry the correlation id as a tag (so checkpoints are queryable by it too).
        List<Activity> checkpoints = spans.Where(a => a.OperationName == "workflow.checkpoint").ToList();
        checkpoints.ShouldNotBeEmpty();
        checkpoints.ShouldAllBe(a => (a.GetTagItem(ArazzoTelemetry.CorrelationIdTag) as string) == correlationId);
    }
}
