// <copyright file="DraftRunTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The §18 draft-run carrier (workflow-designer design §18, staging item 3): starting a draft run captures the
/// draft into the sibling store and enqueues an ordinary Pending run — reserved workflow id, pinned environment,
/// row-scoped to its working copy — and the runs surfaces never leak it.
/// </summary>
[TestClass]
public sealed class DraftRunTests
{
    private const string DocumentJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Draft", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "adopt", "steps": [ { "stepId": "getPet", "operationId": "getPet" } ] } ]
        }
        """;

    private static readonly byte[] DocumentUtf8 = Encoding.UTF8.GetBytes(DocumentJson);
    private static readonly List<KeyValuePair<string, byte[]>> Sources = [new("petstore", Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Pets","version":"1.0.0"},"paths":{}}"""))];

    private static DraftRunStart Start() => new(
        WorkingCopyId: "wc-1",
        WorkflowId: "adopt",
        DocumentUtf8: DocumentUtf8,
        Sources: Sources,
        Environment: "development",
        DocumentEtag: "etag-7",
        StartedBy: "alice");

    [TestMethod]
    public async Task Start_captures_the_draft_and_enqueues_an_environment_pinned_pending_run()
    {
        var store = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var management = new DraftRunManagement(store, drafts);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        WorkflowRunId id = await management.StartAsync(Start(), inputs.RootElement);

        // The audited capture is durable, keyed by the run id, before the run is claimable.
        using (ParsedJsonDocument<DraftRun>? record = await drafts.GetAsync(id, default))
        {
            record.ShouldNotBeNull();
            DraftRun draft = record.RootElement;
            draft.RunIdValue.ShouldBe(id.Value);
            draft.WorkingCopyIdValue.ShouldBe("wc-1");
            ((string)draft.WorkflowId).ShouldBe("adopt");
            ((string)draft.DocumentEtag).ShouldBe("etag-7");
            ((string)draft.Environment).ShouldBe("development");
            ((string)draft.StartedBy).ShouldBe("alice");
            draft.ContentHashValue.ShouldBe(WorkflowPackage.ComputeContentHash(DocumentUtf8, Sources));
        }

        // The captured package unpacks back to the document + sources the run was started from.
        ReadOnlyMemory<byte>? package = await drafts.GetPackageAsync(id, default);
        package.ShouldNotBeNull();
        WorkflowPackageContents contents = WorkflowPackage.Open(package.Value);
        contents.Workflow.ShouldBe(DocumentUtf8);
        contents.Sources.ShouldHaveSingleItem().Key.ShouldBe("petstore");

        // The run record rides the ordinary machinery: Pending, reserved id, pinned environment, inputs aboard,
        // and row-scoped to its working copy.
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(store, id);
        run.ShouldNotBeNull();
        run!.Status.ShouldBe(WorkflowRunStatus.Pending);
        run.WorkflowId.ShouldBe(DraftRuns.RunWorkflowId);
        run.Environment.ShouldBe("development");
        run.Inputs.GetProperty("petId"u8).GetString().ShouldBe("42");
        run.SecurityTags.ToList().ShouldContain(new SecurityTag(DraftRuns.WorkingCopyTagKey, "wc-1"));
    }

    [TestMethod]
    public async Task Start_requires_an_environment_pin()
    {
        var management = new DraftRunManagement(new InMemoryWorkflowStateStore(), new InMemoryDraftRunStore());
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await management.StartAsync(Start() with { Environment = string.Empty }, inputs.RootElement));
    }

    [TestMethod]
    public async Task Draft_runs_never_surface_on_the_runs_listing_even_for_full_reach()
    {
        var store = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var draftManagement = new DraftRunManagement(store, drafts);
        var management = new SecuredWorkflowManagement(store, owner: "ops");

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));
        WorkflowRunId draftId = await draftManagement.StartAsync(Start(), inputs.RootElement);
        WorkflowRunId catalogId = await management.StartAsync("adopt-v1", inputs.RootElement, correlationId: null, tags: default, securityTags: default, environment: "development", default);

        // The production runs listing (unfiltered, full reach) never contains the draft run.
        using (WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Limit: 10), AccessContext.System, default))
        {
            page.Runs.Select(r => r.Id).ShouldBe([catalogId]);
        }

        // The reserved id must be asked for explicitly (the debug-run surface's own view will).
        using (WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(WorkflowId: DraftRuns.RunWorkflowId), AccessContext.System, default))
        {
            page.Runs.Select(r => r.Id).ShouldBe([draftId]);
        }
    }

    [TestMethod]
    public async Task A_draft_run_is_row_scoped_to_its_working_copy()
    {
        var store = new InMemoryWorkflowStateStore();
        var draftManagement = new DraftRunManagement(store, new InMemoryDraftRunStore());
        var management = new SecuredWorkflowManagement(store, owner: "ops");

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));
        WorkflowRunId id = await draftManagement.StartAsync(Start(), inputs.RootElement, securityTags: SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]));

        // A working-copy-scoped grant admits the run (the §14.2 machinery the debug-run surface will resolve through).
        AccessContext workingCopyReach = Scope("sys:workingCopy == 'wc-1'");
        (await management.GetAsync(id, workingCopyReach, default)).ShouldNotBeNull();

        // A grant scoped to a DIFFERENT working copy does not (non-disclosing null, §14.2).
        (await management.GetAsync(id, Scope("sys:workingCopy == 'wc-2'"), default)).ShouldBeNull();

        // And an ordinary production principal (a tenant rule that says nothing about working copies) is denied
        // by the deny-by-default evaluator — draft runs are invisible to production views.
        AccessContext tenantReach = AccessContext.Uniform(
            new SecurityFilter(
                [SecurityRule.Compile("tenant == $claim.tenant")],
                new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = ["acme"] }));
        (await management.GetAsync(id, tenantReach, default)).ShouldBeNull();
    }

    [TestMethod]
    public void An_unpinned_dispatcher_is_rejected_at_construction()
    {
        var store = new InMemoryWorkflowStateStore();

        // Fail-closed (§5.5/§18): a runner must declare the environment it serves — an unscoped dispatcher could
        // otherwise claim any environment's runs (incl. draft runs), crossing the credential boundary. Rejected here,
        // at construction, rather than silently claiming nothing.
        Should.Throw<ArgumentException>(() => new WorkflowDispatcher(store, "runner-1"));
    }

    private static AccessContext Scope(string rule) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>()));
}