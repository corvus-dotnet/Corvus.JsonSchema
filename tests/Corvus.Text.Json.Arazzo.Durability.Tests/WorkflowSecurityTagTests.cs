// <copyright file="WorkflowSecurityTagTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the run security-tag data model (§14.2 slice 1): KVP labels set at creation are persisted in the
/// checkpoint and survive the serialize → store → resume / read-back round-trip, distinct from the free-form
/// user tags. (Row authorization over these tags is a later slice.)
/// </summary>
[TestClass]
public sealed class WorkflowSecurityTagTests
{
    private static readonly TestTimeProvider Time = new(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task Security_tags_set_at_creation_survive_serialize_and_resume()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        SecurityTag[] security = [new("tenant", "acme"), new("team", "payments"), new("team", "billing")];
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, Time, tags: TagSet.FromTags(["nightly"]), securityTags: SecurityTagSet.FromTags(security)))
        {
            run.SecurityTags.ToList().ShouldBe(security);
            await run.EnqueueAsync(default);
        }

        // Re-enter from the persisted checkpoint: the security tags (and the separate user tags) are restored.
        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, "run-1", Time, default);
        resumed.ShouldNotBeNull();
        resumed.SecurityTags.ToList().ShouldBe(security);
        resumed.Tags.ToList().ShouldBe(["nightly"]);
    }

    [TestMethod]
    public async Task The_management_client_surfaces_security_tags_on_run_detail()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        using (WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, Time, securityTags: SecurityTagSet.FromTags([new("tenant", "acme")])))
        {
            await run.EnqueueAsync(default);
        }

        WorkflowRunDetail? detail = await management.GetAsync("run-1", AccessContext.System, default);

        detail.ShouldNotBeNull();
        detail.Value.SecurityTags.ToList().ShouldBe([new SecurityTag("tenant", "acme")]);
    }

    [TestMethod]
    public void A_run_without_security_tags_reports_none()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        using WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, Time);

        run.SecurityTags.IsEmpty.ShouldBeTrue();
    }
}