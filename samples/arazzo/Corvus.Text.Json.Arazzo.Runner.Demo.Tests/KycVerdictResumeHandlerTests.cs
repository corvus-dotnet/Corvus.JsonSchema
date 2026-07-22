// <copyright file="KycVerdictResumeHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using NModels = Corvus.Text.Json.Arazzo.Samples.Notifications.Models;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The runner-host side of the async KYC exchange: <see cref="KycVerdictResumeHandler"/> receives a
/// verdict from the broker and delivers it over the shared durable store, resuming ONLY the run
/// suspended awaiting that account's verdict (matched by channel + account-id correlation).
/// </summary>
[TestClass]
public sealed class KycVerdictResumeHandlerTests
{
    [TestMethod]
    public async Task A_verdict_resumes_only_the_run_correlated_to_its_account()
    {
        var store = new InMemoryWorkflowStateStore();
        using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
        {
            using WorkflowRun a = WorkflowRun.CreateNew(store, "run-acct-1", "onboard", inputs.RootElement, "development");
            await a.SuspendForMessageAsync(1, "kyc.verdict", "acct-1", default);
            using WorkflowRun b = WorkflowRun.CreateNew(store, "run-acct-2", "onboard", inputs.RootElement, "development");
            await b.SuspendForMessageAsync(1, "kyc.verdict", "acct-2", default);
        }

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (run, ct) =>
        {
            resumedRuns.Add(run.Id.Value);
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        var worker = new WorkflowWorker(store, "test-runner");
        var handler = new KycVerdictResumeHandler(worker, resumer, "development", NullLogger<KycVerdictResumeHandler>.Instance);

        // A verdict for an account nobody awaits resumes nothing — both runs stay suspended.
        using (ParsedJsonDocument<NModels.KycVerdictPayload> stray = ParsedJsonDocument<NModels.KycVerdictPayload>.Parse(
            """{"accountId":"acct-9","verified":true,"score":0.5}"""u8.ToArray()))
        {
            await handler.HandleKycVerdictAsync(stray.RootElement);
        }

        resumedRuns.ShouldBeEmpty();

        // acct-1's verdict resumes exactly the run correlated to it; acct-2's run stays put.
        using (ParsedJsonDocument<NModels.KycVerdictPayload> verdict = ParsedJsonDocument<NModels.KycVerdictPayload>.Parse(
            """{"accountId":"acct-1","verified":true,"score":0.98}"""u8.ToArray()))
        {
            await handler.HandleKycVerdictAsync(verdict.RootElement);
        }

        resumedRuns.ShouldBe(["run-acct-1"]);
        using (WorkflowRun? untouched = await WorkflowRun.ResumeAsync(store, "run-acct-2"))
        {
            untouched.ShouldNotBeNull();
            untouched!.Status.ShouldBe(WorkflowRunStatus.Suspended);
            untouched.Wait!.Value.CorrelationId.ShouldBe("acct-2");
        }

        // The resumed run reached its terminal state through the injected resumer.
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(store, "run-acct-1");
        completed.ShouldNotBeNull();
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_verdict_without_an_account_id_is_a_channel_wide_delivery()
    {
        // The platform semantic (the same one the §18 inject endpoint documents): correlation is
        // OPTIONAL — a delivery that carries none matches ANY wait on the channel. A verdict
        // missing its accountId therefore resumes the awaiting run rather than being dropped;
        // the correlation only narrows delivery when it is present.
        var store = new InMemoryWorkflowStateStore();
        using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
        {
            using WorkflowRun a = WorkflowRun.CreateNew(store, "run-corr", "onboard", inputs.RootElement, "development");
            await a.SuspendForMessageAsync(1, "kyc.verdict", "acct-1", default);
        }

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (run, ct) =>
        {
            resumedRuns.Add(run.Id.Value);
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        var worker = new WorkflowWorker(store, "test-runner");
        var handler = new KycVerdictResumeHandler(worker, resumer, "development", NullLogger<KycVerdictResumeHandler>.Instance);

        using ParsedJsonDocument<NModels.KycVerdictPayload> anonymous = ParsedJsonDocument<NModels.KycVerdictPayload>.Parse(
            """{"verified":false,"score":0.1}"""u8.ToArray());
        await handler.HandleKycVerdictAsync(anonymous.RootElement);

        resumedRuns.ShouldBe(["run-corr"]);
    }

    [TestMethod]
    public async Task A_verdict_does_not_resume_a_run_pinned_to_another_environment()
    {
        var store = new InMemoryWorkflowStateStore();
        using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
        {
            // Two runs await the SAME account's verdict, one pinned to this runner's environment ("development"), one to another.
            using WorkflowRun mine = WorkflowRun.CreateNew(store, "run-dev", "onboard", inputs.RootElement, "development");
            await mine.SuspendForMessageAsync(1, "kyc.verdict", "acct-1", default);
            using WorkflowRun other = WorkflowRun.CreateNew(store, "run-prod", "onboard", inputs.RootElement, "production");
            await other.SuspendForMessageAsync(1, "kyc.verdict", "acct-1", default);
        }

        var resumedRuns = new List<string>();
        WorkflowResumer resumer = async (run, ct) =>
        {
            resumedRuns.Add(run.Id.Value);
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        };

        var worker = new WorkflowWorker(store, "test-runner");
        var handler = new KycVerdictResumeHandler(worker, resumer, "development", NullLogger<KycVerdictResumeHandler>.Instance);

        // The development runner's verdict consumer resumes ONLY the development-pinned run, never the production-pinned
        // one that awaits the same channel and correlation (§5.5 message-delivery credential boundary).
        using ParsedJsonDocument<NModels.KycVerdictPayload> verdict = ParsedJsonDocument<NModels.KycVerdictPayload>.Parse(
            """{"accountId":"acct-1","verified":true,"score":0.98}"""u8.ToArray());
        await handler.HandleKycVerdictAsync(verdict.RootElement);

        resumedRuns.ShouldBe(["run-dev"]);
    }
}
