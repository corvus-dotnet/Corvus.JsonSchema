// <copyright file="ServerlessWorkflowRunHostTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the <see cref="ServerlessWorkflowRunHost"/> is the function-side counterpart to the in-process resumer:
/// invoked with only a run id it restores the run from the store, resolves its (baked) workflow, binds transports,
/// and advances it — and it re-checks dispatchability so an at-least-once duplicate invocation of a settled run is a
/// safe no-op rather than a re-run.
/// </summary>
[TestClass]
public class ServerlessWorkflowRunHostTests
{
    private static readonly TimeProvider Time = TimeProvider.System;

    private static readonly WorkflowTransports EmptyTransports =
        new(ImmutableDictionary<string, IApiTransport>.Empty, WorkflowTransports.NoMessageTransports);

    [TestMethod]
    public async Task Advances_a_fresh_run_binding_transports_and_running_the_resolved_workflow()
    {
        var store = new InMemoryWorkflowStateStore();
        await SeedPendingRun(store, "run-1", "wf");
        var workflow = new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed);
        string? boundWorkflowId = null;
        WorkflowTransports Binder(WorkflowDescriptor descriptor, SecurityTagSet tags)
        {
            boundWorkflowId = descriptor.WorkflowId;
            return EmptyTransports;
        }

        var host = new ServerlessWorkflowRunHost(store, new BakedHostedWorkflowResolver(workflow), Binder, Time);

        WorkflowRunResultKind? result = await host.InvokeAsync(TestAddresses.Dev("run-1"), default);

        // The invocation restored the run, bound its transports, ran the resolved workflow, and returned its outcome.
        result.ShouldBe(WorkflowRunResultKind.Completed);
        workflow.Ran.ShouldBeTrue();
        boundWorkflowId.ShouldBe("wf");
    }

    [TestMethod]
    public async Task Advances_a_run_the_control_plane_marked_resume_requested()
    {
        var store = new InMemoryWorkflowStateStore();
        await SeedSuspendedRun(store, "run-1", "wf", resumeRequested: true);
        var workflow = new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed);

        var host = new ServerlessWorkflowRunHost(store, new BakedHostedWorkflowResolver(workflow), NoTransports, Time);

        WorkflowRunResultKind? result = await host.InvokeAsync(TestAddresses.Dev("run-1"), default);

        // A Suspended run the control plane requested a resume for (§18) is dispatchable — it advances.
        result.ShouldBe(WorkflowRunResultKind.Completed);
        workflow.Ran.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_suspended_run_without_a_resume_request_is_a_guarded_no_op()
    {
        var store = new InMemoryWorkflowStateStore();
        await SeedSuspendedRun(store, "run-1", "wf", resumeRequested: false);
        var workflow = new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed);

        var host = new ServerlessWorkflowRunHost(store, new BakedHostedWorkflowResolver(workflow), NoTransports, Time);

        WorkflowRunResultKind? result = await host.InvokeAsync(TestAddresses.Dev("run-1"), default);

        // A run merely waiting (no resume request) is not something this host advances: null, and the workflow never ran.
        result.ShouldBeNull();
        workflow.Ran.ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_missing_run_is_a_guarded_no_op()
    {
        var store = new InMemoryWorkflowStateStore();
        var workflow = new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed);

        var host = new ServerlessWorkflowRunHost(store, new BakedHostedWorkflowResolver(workflow), NoTransports, Time);

        WorkflowRunResultKind? result = await host.InvokeAsync(TestAddresses.Dev("does-not-exist"), default);

        // A deleted/unknown run (an at-least-once duplicate that outlived the run) resolves to nothing: null, never run.
        result.ShouldBeNull();
        workflow.Ran.ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_run_misrouted_from_a_different_version_is_faulted_and_not_invoked_again_for_ever()
    {
        var inner = new InMemoryWorkflowStateStore();
        await SeedPendingRun(inner, "run-1", "wf-other");
        var store = new FlushCountingStore(inner);
        var baked = new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed);
        var host = new ServerlessWorkflowRunHost(store, new BakedHostedWorkflowResolver(baked), NoTransports, Time);

        // The run is dispatchable, so it reaches the resolver, which is baked for a different version and refuses it
        // rather than silently running the wrong workflow. The refusal is the same on every invocation, so it ends the
        // run. Thrown to the dispatching runner instead, the run stays Pending and is dispatched here again each poll.
        WorkflowRunResultKind? kind = await host.InvokeAsync(TestAddresses.Dev("run-1"), default);

        kind.ShouldBe(WorkflowRunResultKind.Faulted);
        baked.Ran.ShouldBeFalse();

        // The fault is what the runner is told about, so a deferring store has landed it before the outcome returns.
        store.Flushes.ShouldBe(1);
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(inner, TestAddresses.Dev("run-1"), Time, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Faulted);
        reloaded.Fault!.Value.Error.ShouldBe("executor-unresolvable");

        // And the run is no longer dispatchable: a second invocation is the guarded no-op.
        (await host.InvokeAsync(TestAddresses.Dev("run-1"), default)).ShouldBeNull();
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        var store = new InMemoryWorkflowStateStore();
        var resolver = new BakedHostedWorkflowResolver(new RecordingHostedWorkflow("wf", WorkflowRunResultKind.Completed));

        Should.Throw<ArgumentNullException>(() => new ServerlessWorkflowRunHost(null!, resolver, NoTransports));
        Should.Throw<ArgumentNullException>(() => new ServerlessWorkflowRunHost(store, null!, NoTransports));
        Should.Throw<ArgumentNullException>(() => new ServerlessWorkflowRunHost(store, resolver, null!));
    }

    private static WorkflowTransports NoTransports(WorkflowDescriptor descriptor, SecurityTagSet tags) => EmptyTransports;

    private static async Task SeedPendingRun(IWorkflowStateStore store, string runId, string workflowId)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, "development", Time);
        await run.EnqueueAsync(default);
    }

    private static async Task SeedSuspendedRun(IWorkflowStateStore store, string runId, string workflowId, bool resumeRequested)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, "development", Time);
        await run.SuspendForTimerAsync(cursor: 1, TimeSpan.FromMinutes(5), default);
        if (resumeRequested)
        {
            await run.RequestResumeAsync(null, default);
        }
    }

    // A store that counts the flushes asked of it, standing in for the serverless HTTP store's deferred saves.
    private sealed class FlushCountingStore(IWorkflowCheckpointStore inner) : IWorkflowCheckpointStore, IWorkflowCheckpointFlush
    {
        public int Flushes { get; private set; }

        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => inner.SaveAsync(address, checkpointUtf8, index, expected, cancellationToken);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
            => inner.LoadAsync(address, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            this.Flushes++;
            return ValueTask.CompletedTask;
        }
    }

    // A minimal in-hand executor: it records that it ran and returns a configured outcome. It advertises only its
    // versioned id (all the resolver reads); the transports it receives are the empty maps the test binder returns.
    private sealed class RecordingHostedWorkflow(string workflowId, WorkflowRunResultKind kind) : IHostedWorkflow
    {
        public bool Ran { get; private set; }

        public WorkflowDescriptor Descriptor { get; } = new(workflowId, [], []);

        public ValueTask<WorkflowRunResultKind> RunAsync(
            IReadOnlyDictionary<string, IApiTransport> apiTransports,
            IReadOnlyDictionary<string, IMessageTransport> messageTransports,
            JsonWorkspace workspace,
            JsonElement inputs,
            IWorkflowRun run,
            CancellationToken cancellationToken)
        {
            this.Ran = true;
            return new ValueTask<WorkflowRunResultKind>(kind);
        }
    }
}