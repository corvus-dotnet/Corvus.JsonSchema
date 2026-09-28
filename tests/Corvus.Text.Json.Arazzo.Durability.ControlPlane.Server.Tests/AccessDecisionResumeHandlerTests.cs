// <copyright file="AccessDecisionResumeHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SwModels = Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The system-runner side of the access-decision exchange (design §16.5.1): <see cref="AccessDecisionResumeHandler"/>
/// receives a decision from the broker and delivers it over the shared durable store, resuming ONLY the approval run
/// suspended awaiting that request's decision (matched by channel + request-id correlation).
/// </summary>
[TestClass]
public sealed class AccessDecisionResumeHandlerTests
{
    [TestMethod]
    public async Task A_decision_resumes_only_the_run_correlated_to_its_request()
    {
        var store = new InMemoryWorkflowStateStore();
        using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
        {
            using WorkflowRun a = WorkflowRun.CreateNew(store, "run-req-1", "access-approval", inputs.RootElement, "system");
            await a.SuspendForMessageAsync(1, "access.decision", "req-1", default);
            using WorkflowRun b = WorkflowRun.CreateNew(store, "run-req-2", "access-approval", inputs.RootElement, "system");
            await b.SuspendForMessageAsync(1, "access.decision", "req-2", default);
        }

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (run, ct) =>
        {
            resumedRuns.Add(run.Id.Value);
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        var worker = new WorkflowWorker(store, "system-runner");
        var handler = new AccessDecisionResumeHandler(
            new StoreWorkflowMessageDelivery(worker, resumer, "system"),
            NullLogger<AccessDecisionResumeHandler>.Instance,
            new ImmediateTimeProvider());

        // A decision for a request nobody awaits resumes nothing — both runs stay suspended.
        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> stray = Decision("req-9"))
        {
            await handler.HandleAccessDecisionAsync(stray.RootElement);
        }

        resumedRuns.ShouldBeEmpty();

        // req-1's decision resumes exactly the run correlated to it; req-2's run stays put.
        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> decision = Decision("req-1"))
        {
            await handler.HandleAccessDecisionAsync(decision.RootElement);
        }

        resumedRuns.ShouldBe(["run-req-1"]);
    }

    [TestMethod]
    public async Task A_decision_does_not_resume_an_approval_run_pinned_to_another_environment()
    {
        var store = new InMemoryWorkflowStateStore();
        using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
        {
            // Two runs await the SAME request's decision, one pinned to this runner's environment ("system"), one to another.
            using WorkflowRun mine = WorkflowRun.CreateNew(store, "run-system", "access-approval", inputs.RootElement, "system");
            await mine.SuspendForMessageAsync(1, "access.decision", "req-1", default);
            using WorkflowRun other = WorkflowRun.CreateNew(store, "run-dev", "access-approval", inputs.RootElement, "development");
            await other.SuspendForMessageAsync(1, "access.decision", "req-1", default);
        }

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (run, ct) =>
        {
            resumedRuns.Add(run.Id.Value);
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        var worker = new WorkflowWorker(store, "system-runner");
        var handler = new AccessDecisionResumeHandler(
            new StoreWorkflowMessageDelivery(worker, resumer, "system"),
            NullLogger<AccessDecisionResumeHandler>.Instance,
            new ImmediateTimeProvider());

        // The system runner's decision consumer resumes ONLY the system-pinned approval run, never the development-pinned
        // one that awaits the same channel and correlation (§5.5 message-delivery credential boundary).
        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> decision = Decision("req-1"))
        {
            await handler.HandleAccessDecisionAsync(decision.RootElement);
        }

        resumedRuns.ShouldBe(["run-system"]);
    }

    [TestMethod]
    public async Task A_decision_that_arrives_before_its_run_suspends_resumes_the_run_once_it_does()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(store, "run-req-1", "access-approval", inputs.RootElement, "system");

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (r, ct) =>
        {
            resumedRuns.Add(r.Id.Value);
            await r.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        // The run has sent its approval-required notification but not yet suspended when the decision arrives. It
        // suspends during the handler's first pause, which is the gap a prompt administrator's decision falls into.
        var time = new ImmediateTimeProvider(() => run.SuspendForMessageAsync(1, "access.decision", "req-1", default).AsTask());
        var handler = new AccessDecisionResumeHandler(
            new StoreWorkflowMessageDelivery(new WorkflowWorker(store, "system-runner"), resumer, "system"),
            NullLogger<AccessDecisionResumeHandler>.Instance,
            time);

        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> decision = Decision("req-1"))
        {
            await handler.HandleAccessDecisionAsync(decision.RootElement);
        }

        resumedRuns.ShouldBe(["run-req-1"]);
        time.Delays.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_decision_no_run_awaits_is_given_up_within_the_brokers_acknowledgement_deadline()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowResumer resumer = (_, _) => throw new InvalidOperationException("Nothing awaits the decision, so nothing resumes.");
        var time = new ImmediateTimeProvider();
        var handler = new AccessDecisionResumeHandler(
            new StoreWorkflowMessageDelivery(new WorkflowWorker(store, "system-runner"), resumer, "system"),
            NullLogger<AccessDecisionResumeHandler>.Instance,
            time);

        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> decision = Decision("req-9"))
        {
            await handler.HandleAccessDecisionAsync(decision.RootElement);
        }

        // It tried again, and stopped well inside the NATS transport's thirty-second acknowledgement deadline, past which
        // the broker would redeliver the message while the handler still held it.
        time.Delays.ShouldNotBeEmpty();
        TimeSpan total = TimeSpan.Zero;
        foreach (TimeSpan delay in time.Delays)
        {
            total += delay;
        }

        total.ShouldBeLessThan(TimeSpan.FromSeconds(25));
    }

    // Build the decision the same typed, owning-document way the producer does — no hand-rolled JSON string.
    private static ParsedJsonDocument<SwModels.AccessDecisionPayload> Decision(string requestId)
        => SwModels.AccessDecisionPayload.Create(
            decidedBy: "admin",
            outcome: SwModels.AccessDecisionPayload.OutcomeEntity.EnumValues.Approved,
            requestId: requestId);

    /// <summary>
    /// Fires every timer at once, recording its due time, after running an optional hook on the first one. That lets a
    /// test put an event (a run suspending) inside the handler's first pause without waiting real time.
    /// </summary>
    private sealed class ImmediateTimeProvider(Func<Task>? onFirstPause = null) : TimeProvider
    {
        private Func<Task>? onFirstPause = onFirstPause;

        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            this.Delays.Add(dueTime);
            Func<Task>? hook = this.onFirstPause;
            this.onFirstPause = null;
            _ = Task.Run(async () =>
            {
                if (hook is not null)
                {
                    await hook();
                }

                callback(state);
            });

            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}