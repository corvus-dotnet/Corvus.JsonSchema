// <copyright file="IdempotentStartDerivationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Pins ADR 0065 §9's idempotent-id derivation against H18: the id is a keyed MAC over
/// (ownerGroup, environment, workflowId, idempotencyKey), so the same business key names different runs in
/// different environments, and an adversary cannot compute a victim's run id offline and pre-create it.
/// </summary>
[TestClass]
public sealed class IdempotentStartDerivationTests
{
    private static readonly byte[] DerivationKey = RandomNumberGenerator.GetBytes(WorkflowRunDerivation.MinimumKeyBytes);

    [TestMethod]
    public async Task The_same_business_key_in_two_environments_names_two_runs()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: new WorkflowRunDerivation(DerivationKey));

        WorkflowRunId a = (await management.StartIdempotentAsync("wf-v1", default, "order-42", "development")).RunId;
        WorkflowRunId b = (await management.StartIdempotentAsync("wf-v1", default, "order-42", "production")).RunId;

        // One business key, two environments: two runs (the finding: today both derive one id, so the production
        // start silently returns the development run and never executes).
        b.ShouldNotBe(a);
        (await store.LoadAsync(TestAddresses.Dev(a), default)).ShouldNotBeNull();
        (await store.LoadAsync(TestAddresses.In("production", b), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task The_idempotent_id_is_not_computable_without_the_deployment_key()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: new WorkflowRunDerivation(DerivationKey));

        // The audit's offline computation (UO-2): SHA-256(workflowId ‖ 0x00 ‖ idempotencyKey), truncated to the
        // grammar — everything an adversary needs is public, so they can pre-create the id and make a victim's
        // legitimate start return success and never execute.
        byte[] material = Encoding.UTF8.GetBytes("wf-v1\0order-42");
        string offline = Convert.ToHexStringLower(SHA256.HashData(material)[..16]);

        WorkflowRunId minted = (await management.StartIdempotentAsync("wf-v1", default, "order-42", "development")).RunId;

        minted.Value.ShouldNotBe(offline);
        WorkflowRunId.IsWellFormed(minted.Value).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_redelivered_start_converges_and_reports_the_existing_run()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: new WorkflowRunDerivation(DerivationKey));

        IdempotentStartResult first = await management.StartIdempotentAsync("wf-v1", default, "order-42", "development");
        IdempotentStartResult again = await management.StartIdempotentAsync("wf-v1", default, "order-42", "development");

        // The distinguishable result (the audit's criterion): the redelivery converges on the same run and SAYS it
        // did, where the old surface answered indistinguishable success for both.
        first.Created.ShouldBeTrue();
        again.Created.ShouldBeFalse();
        again.RunId.ShouldBe(first.RunId);
    }

    [TestMethod]
    public async Task A_run_occupying_the_derived_id_that_is_not_this_start_is_refused()
    {
        var store = new InMemoryWorkflowStateStore();
        var derivation = new WorkflowRunDerivation(DerivationKey);
        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: derivation);

        // The pre-created-id attack: an unrelated run squatting the id this start derives to. Before the fix the
        // start swallowed the conflict and answered success with the squatter's id — the victim's run never existed
        // and nothing said so.
        WorkflowRunId derived = derivation.IdempotentStart(null, "development", "wf-v1", "order-42");
        using (WorkflowRun squat = WorkflowRun.CreateNew(store, derived, "other-wf", default, "development"))
        {
            await squat.EnqueueAsync(default);
        }

        await Should.ThrowAsync<WorkflowRunCollisionException>(
            async () => await management.StartIdempotentAsync("wf-v1", default, "order-42", "development"));
    }

    [TestMethod]
    public async Task A_named_start_converges_only_on_its_own_logical_start()
    {
        var store = new InMemoryWorkflowStateStore();
        var derivation = new WorkflowRunDerivation(DerivationKey);
        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: derivation);
        WorkflowRunId id = derivation.ScheduleAddress("nightly");

        (await management.StartNamedAsync(id, "wf-v1", default, "development")).Created.ShouldBeTrue();
        (await management.StartNamedAsync(id, "wf-v1", default, "development")).Created.ShouldBeFalse();

        // Under the composite (environment, runId) key (ADR 0065 decision 9) the same id presented from another
        // environment names a DIFFERENT run: the development occupant is invisible there — no collision, no
        // existence oracle — and a fresh run is created at the other address. Schedule ids stay globally unique
        // through the schedule REGISTRY, whose insert conflict is the schedules surface's 409 (piece 3 / C2);
        // the run key no longer carries that.
        (await management.StartNamedAsync(id, "wf-v1", default, "production")).Created.ShouldBeTrue();

        // Within the environment, an occupant that is NOT this logical start (another workflow) is refused —
        // never a silent adoption of the existing run.
        await Should.ThrowAsync<WorkflowRunCollisionException>(
            async () => await management.StartNamedAsync(id, "other-wf", default, "development"));
    }

    [TestMethod]
    public async Task A_named_start_outside_the_grammar_is_refused()
    {
        var management = new SecuredWorkflowManagement(new InMemoryWorkflowStateStore(), "ops", runDerivation: new WorkflowRunDerivation(DerivationKey));

        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(
            async () => await management.StartNamedAsync(new WorkflowRunId("run-1"), "wf-v1", default, "development"));
        refusal.Message.ShouldContain("32 lowercase hex");
    }

    [TestMethod]
    public async Task An_idempotent_start_without_the_deployment_key_is_refused()
    {
        var management = new SecuredWorkflowManagement(new InMemoryWorkflowStateStore(), "ops");

        // Fail closed: no key means no idempotent starts, never an unkeyed derivation.
        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(
            async () => await management.StartIdempotentAsync("wf-v1", default, "order-42", "development"));
        refusal.Message.ShouldContain("run-derivation");
    }

    [TestMethod]
    public async Task The_environments_owner_group_is_part_of_the_derivation()
    {
        var store = new InMemoryWorkflowStateStore();
        var derivation = new WorkflowRunDerivation(DerivationKey);
        var environments = new Environments.InMemoryEnvironmentStore();
        using (ParsedJsonDocument<Environments.Environment> draft = Environments.Environment.Draft(
            "production", null, null, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")])))
        {
            using ParsedJsonDocument<Environments.Environment> added = await environments.AddAsync(draft.RootElement, "ops", default);
        }

        var management = new SecuredWorkflowManagement(store, "ops", runDerivation: derivation, environments: environments);

        WorkflowRunId minted = (await management.StartIdempotentAsync("wf-v1", default, "order-42", "production")).RunId;

        // The minted id IS the full-tuple derivation with the environment's owner group resolved from its record —
        // and not the group-less one, so tenancy is bound into the id when the deployment governs it.
        minted.ShouldBe(derivation.IdempotentStart("acme", "production", "wf-v1", "order-42"));
        minted.ShouldNotBe(derivation.IdempotentStart(null, "production", "wf-v1", "order-42"));
    }
}
