// <copyright file="RunnerHostedVersionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The runner's single answer to "what have I baked" (ADR 0065). Every path that claims work asks it, so what matters
/// is that they all get the same answer, that a hot path does not pay for it per call, and that it still moves when the
/// deployment does.
/// </summary>
[TestClass]
public sealed class RunnerHostedVersionsTests
{
    [TestMethod]
    public async Task The_resolved_versions_become_versioned_workflow_ids()
    {
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", RunnerApiFixture.Production);

        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: false);

        IReadOnlyList<string> ids = await hosted.GetAsync(default);

        // SeedCatalogAsync adds version 1, and the provider's whole job on this path is turning the control plane's
        // (baseWorkflowId, versionNumber) pair into the versioned id a claim matches on.
        ids.ShouldBe(["adopt-v1"]);
    }

    [TestMethod]
    public async Task A_runner_that_serves_schedules_adds_the_reserved_id()
    {
        // A durable schedule is a run of the built-in scheduler workflow, not a catalogued version, so the control
        // plane's listing can never contain it. Without this the schedule run is never claimable and the cron silently
        // never fires.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();

        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: true);

        (await hosted.GetAsync(default)).ShouldContain(ScheduleHostedWorkflow.ScheduleWorkflowId);
    }

    [TestMethod]
    public async Task A_runner_that_does_not_serve_schedules_omits_it()
    {
        // A schedule run claimed by a runner with no scheduler wired into its resumer would simply fault.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();

        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: false);

        (await hosted.GetAsync(default)).ShouldNotContain(ScheduleHostedWorkflow.ScheduleWorkflowId);
    }

    [TestMethod]
    public async Task Repeated_calls_inside_the_window_do_not_re_resolve()
    {
        // Message delivery asks per message. Resolving each time would put a round trip on every delivery and spend a
        // catalog quota on it.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", RunnerApiFixture.Production);

        var clock = new TestClock(RunnerApiFixture.T0);
        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: false, TimeSpan.FromSeconds(5), clock);

        IReadOnlyList<string> first = await hosted.GetAsync(default);
        IReadOnlyList<string> second = await hosted.GetAsync(default);

        // The same instance, so nothing was resolved the second time.
        second.ShouldBeSameAs(first);
    }

    [TestMethod]
    public async Task The_window_expiring_picks_up_a_newly_published_version()
    {
        // The reason it refreshes rather than being held: a publish must reach a running runner without a restart.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();

        var clock = new TestClock(RunnerApiFixture.T0);
        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: false, TimeSpan.FromSeconds(5), clock);

        (await hosted.GetAsync(default)).ShouldBeEmpty();

        await fixture.SeedCatalogAsync("adopt", RunnerApiFixture.Production);

        // Still the cached answer inside the window.
        (await hosted.GetAsync(default)).ShouldBeEmpty();

        clock.Advance(TimeSpan.FromSeconds(6));
        (await hosted.GetAsync(default)).ShouldBe(["adopt-v1"]);
    }

    [TestMethod]
    public async Task A_burst_on_an_expired_window_resolves_once()
    {
        // The interlock. Without it a burst of messages arriving together on an expired window each start their own
        // listing — the load the cache exists to prevent, at the moment it matters most.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", RunnerApiFixture.Production);

        var clock = new TestClock(RunnerApiFixture.T0);
        var hosted = new RunnerHostedVersions(fixture.Client, servesSchedules: false, TimeSpan.FromSeconds(5), clock);

        IReadOnlyList<string>[] results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(async _ => await hosted.GetAsync(default)));

        results[0].ShouldBe(["adopt-v1"]);

        // Every caller got the identical instance, so exactly one resolution happened.
        foreach (IReadOnlyList<string> result in results)
        {
            result.ShouldBeSameAs(results[0]);
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}