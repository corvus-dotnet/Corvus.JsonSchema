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
        var handler = new AccessDecisionResumeHandler(worker, resumer, "system", NullLogger<AccessDecisionResumeHandler>.Instance);

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
        var handler = new AccessDecisionResumeHandler(worker, resumer, "system", NullLogger<AccessDecisionResumeHandler>.Instance);

        // The system runner's decision consumer resumes ONLY the system-pinned approval run, never the development-pinned
        // one that awaits the same channel and correlation (§5.5 message-delivery credential boundary).
        using (ParsedJsonDocument<SwModels.AccessDecisionPayload> decision = Decision("req-1"))
        {
            await handler.HandleAccessDecisionAsync(decision.RootElement);
        }

        resumedRuns.ShouldBe(["run-system"]);
    }

    // Build the decision the same typed, owning-document way the producer does — no hand-rolled JSON string.
    private static ParsedJsonDocument<SwModels.AccessDecisionPayload> Decision(string requestId)
        => SwModels.AccessDecisionPayload.Create(
            decidedBy: "admin",
            outcome: SwModels.AccessDecisionPayload.OutcomeEntity.EnumValues.Approved,
            requestId: requestId);
}