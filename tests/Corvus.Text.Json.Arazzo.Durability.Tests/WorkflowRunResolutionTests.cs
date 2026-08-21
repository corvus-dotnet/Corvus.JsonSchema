// <copyright file="WorkflowRunResolutionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// ADR 0065 §9 (H18 piece 3, C4): the management client resolves a bare run id through the store's reach-filtered
/// index query — the SAME predicate the runs listing uses — so a run's visibility by id can never drift from its
/// visibility in the list. Before this cut, get-by-id decided reach in-process over the checkpoint BODY's tags while
/// the listing decided it in the store over the INDEX entry's tags: two predicates, and a run whose index row was out
/// of reach was hidden by the list yet disclosed by get.
/// </summary>
[TestClass]
public sealed class WorkflowRunResolutionTests
{
    private static readonly TestTimeProvider Time = new(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));

    private static AccessContext AcmeReader => new(AcmeReach, null, null);

    private static AccessContext AcmeWriter => new(null, AcmeReach, null);

    private static SecurityFilter AcmeReach => new(
        [SecurityRule.Compile("tenant == 'acme'")],
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    [TestMethod]
    public async Task Get_hides_a_run_exactly_as_the_listing_does()
    {
        InMemoryWorkflowStateStore store = await DriftedRunAsync("run-1");
        var management = new SecuredWorkflowManagement(store, owner: "ops");

        // The listing hides the run: its index row's tags are outside the acme reach.
        using WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Limit: 10), AcmeReader, default);
        page.Runs.ShouldBeEmpty();

        // Get-by-id must answer with the SAME predicate: absent, not the detail the body's tags would admit.
        (await management.GetAsync("run-1", AcmeReader, default)).ShouldBeNull();
        (await management.GetStepJournalAsync("run-1", AcmeReader, default)).ShouldBeNull();
        (await management.LoadStateAsync("run-1", AcmeReader, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Cancel_and_delete_refuse_a_run_exactly_as_the_listing_hides_it()
    {
        InMemoryWorkflowStateStore store = await DriftedRunAsync("run-1");
        var management = new SecuredWorkflowManagement(store, owner: "ops");

        // The write verbs resolve through the same index predicate (with the write reach): a run whose index row is
        // out of reach is not actionable, whatever the body's tags say.
        (await management.CancelAsync("run-1", "operator asked", AcmeWriter, default)).ShouldBeFalse();
        (await management.DeleteAsync("run-1", AcmeWriter, default)).ShouldBeFalse();

        // The run itself is untouched (the refusal came before any store write).
        (await store.LoadAsync(TestAddresses.Dev("run-1"), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Get_resolves_a_reserved_kind_run_named_by_id()
    {
        var store = new InMemoryWorkflowStateStore();
        await CreateRunAsync(store, "draft-1", DraftRuns.RunWorkflowId);
        await CreateRunAsync(store, "schedule-1", ScheduleHostedWorkflow.ScheduleWorkflowId);
        var management = new SecuredWorkflowManagement(store, owner: "ops");

        // The browse listing hides the reserved kinds (§18/#896), but a caller that NAMES the run id is at least as
        // explicit as one naming the reserved workflow id: the debug-run and schedule surfaces resolve exactly so.
        using (WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Limit: 10), AccessContext.System, default))
        {
            page.Runs.ShouldBeEmpty();
        }

        (await management.GetAsync("draft-1", AccessContext.System, default)).ShouldNotBeNull();
        (await management.GetAsync("schedule-1", AccessContext.System, default)).ShouldNotBeNull();
    }

    // A run whose checkpoint BODY carries in-reach tags (tenant acme) while its INDEX row carries out-of-reach tags
    // (tenant globex). A trusted writer never produces this — the store takes body and index in one save — so it is
    // the sharpest probe for WHICH predicate answers a bare-id operation: the in-process body check admits it, the
    // index predicate the listing shares does not.
    private static async ValueTask<InMemoryWorkflowStateStore> DriftedRunAsync(string runId)
    {
        var store = new InMemoryWorkflowStateStore();
        await CreateRunAsync(store, runId, "wf", securityTags: SecurityTagSet.FromTags([new("tenant", "acme")]));

        WorkflowCheckpoint? loaded = await store.LoadAsync(TestAddresses.Dev(runId), default);
        loaded.ShouldNotBeNull();
        WorkflowCheckpoint checkpoint = loaded.Value;
        var drifted = new WorkflowRunIndexEntry(
            "wf",
            WorkflowRunStatus.Pending,
            Time.GetUtcNow(),
            Time.GetUtcNow(),
            SecurityTags: SecurityTagSet.FromTags([new("tenant", "globex")]));
        await store.SaveAsync(TestAddresses.Dev(runId), checkpoint.Utf8.ToArray(), drifted, checkpoint.Etag, default);
        return store;
    }

    private static async ValueTask CreateRunAsync(InMemoryWorkflowStateStore store, string runId, string workflowId, SecurityTagSet securityTags = default)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, workflowId, doc.RootElement, "development", Time, securityTags: securityTags);
        await run.EnqueueAsync(default);
    }
}
