// <copyright file="TokenBucketRunnerQuotaGuardTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// The in-process quota guard (ADR 0065 decision 3): what it admits, what it refuses, what the refusal says, and the
/// two properties that are easy to get wrong — that a refusal spends nothing, and that a per-runner refusal does not
/// drain its tenant's aggregate.
/// </summary>
[TestClass]
public sealed class TokenBucketRunnerQuotaGuardTests
{
    private const string Tenant = "acme";
    private const string Runner = "runner-a";
    private const string Peer = "runner-b";

    [TestMethod]
    public async Task A_request_within_the_limit_is_admitted()
    {
        Fixture fixture = Fixture.With(o => o.RunnerCheckpoints = new RunnerQuotaLimit(10, 10));

        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_burst_is_spent_and_then_the_request_is_refused()
    {
        Fixture fixture = Fixture.With(o => o.RunnerCheckpoints = new RunnerQuotaLimit(10, 3));

        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();

        RunnerQuotaRejection? refused = await fixture.CheckpointAsync(Runner);
        refused.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_refusal_names_the_quota_and_the_counter()
    {
        // The whole point of the 429 body: a runner told only "too many requests" can do nothing but back off
        // everything, while one told which dimension and which counter can keep saving checkpoints and slow its claims.
        Fixture fixture = Fixture.With(o => o.RunnerCheckpoints = new RunnerQuotaLimit(1, 1));

        await fixture.CheckpointAsync(Runner);
        RunnerQuotaRejection refused = (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        refused.Quota.ShouldBe("checkpoint-rate/runner");
        refused.Counter.ShouldBe(Runner);
        refused.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [TestMethod]
    public async Task A_tenant_refusal_names_the_owner_group()
    {
        Fixture fixture = Fixture.With(o =>
        {
            o.TenantCheckpoints = new RunnerQuotaLimit(1, 1);
            o.RunnerCheckpoints = RunnerQuotaLimit.None;
        });

        await fixture.CheckpointAsync(Runner);
        RunnerQuotaRejection refused = (await fixture.CheckpointAsync(Peer)).ShouldNotBeNull();

        refused.Quota.ShouldBe("checkpoint-rate/tenant");
        refused.Counter.ShouldBe(Tenant);
    }

    [TestMethod]
    public async Task An_absent_owner_group_is_charged_to_the_deployment()
    {
        // A deployment that publishes nothing to tell owner groups apart has one tenant by construction. The refusal
        // says so rather than carrying an empty counter.
        Fixture fixture = Fixture.With(o =>
        {
            o.TenantCheckpoints = new RunnerQuotaLimit(1, 1);
            o.RunnerCheckpoints = RunnerQuotaLimit.None;
        });

        await fixture.CheckpointAsync(Runner, tenant: null);
        RunnerQuotaRejection refused = (await fixture.CheckpointAsync(Peer, tenant: null)).ShouldNotBeNull();

        refused.Counter.ShouldBe(RunnerQuotaNames.Deployment);
    }

    [TestMethod]
    public async Task Tokens_refill_at_the_configured_rate()
    {
        Fixture fixture = Fixture.With(o => o.RunnerCheckpoints = new RunnerQuotaLimit(2, 2));

        await fixture.CheckpointAsync(Runner);
        await fixture.CheckpointAsync(Runner);
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        // Half a second at two per second is one token.
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(500));
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Refill_never_exceeds_the_burst()
    {
        // An idle counter must not bank an unbounded allowance: a runner quiet for an hour would otherwise be admitted
        // an hour's worth of requests at once, which is the load the limit exists to prevent.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(10, 2);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        // Spend the bucket first, so the hour that follows is refilling a counter that exists rather than deciding what
        // a fresh one starts at. Without this the test would pass on the seed value and assert nothing about refill.
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        fixture.Clock.Advance(TimeSpan.FromHours(1));

        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_refusal_spends_nothing()
    {
        // Charging a refused request would let a caller already at its limit hold itself there by retrying, turning a
        // momentary overshoot into an indefinite one.
        Fixture fixture = Fixture.With(o => o.RunnerCheckpoints = new RunnerQuotaLimit(1, 1));

        await fixture.CheckpointAsync(Runner);
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        // Ten refused attempts, then exactly one second's worth of refill. If the refusals had been charged, this would
        // still be refused.
        for (int i = 0; i < 10; ++i)
        {
            (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_per_runner_refusal_does_not_drain_the_tenant_aggregate()
    {
        // The reason every scope is tested before any is spent. Without it, a runner pinned by its own sub-limit would
        // consume its tenant's whole allowance while never completing a request, and take its well-behaved peers down.
        Fixture fixture = Fixture.With(o =>
        {
            o.TenantCheckpoints = new RunnerQuotaLimit(1, 10);
            o.RunnerCheckpoints = new RunnerQuotaLimit(1, 1);
        });

        await fixture.CheckpointAsync(Runner);
        for (int i = 0; i < 20; ++i)
        {
            (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();
        }

        // The tenant spent one token, for the one admitted request, so nine remain. Each is claimed by a different
        // principal: the per-runner burst is one, so a single peer making nine requests would be refused by its own
        // sub-limit and would prove nothing about the aggregate.
        for (int i = 0; i < 9; ++i)
        {
            (await fixture.CheckpointAsync($"peer-{i}")).ShouldBeNull();
        }

        // And the tenth is refused by the aggregate rather than by any runner's sub-limit, which is what shows the
        // aggregate was spent exactly once by the pinned runner.
        RunnerQuotaRejection exhausted = (await fixture.CheckpointAsync("peer-9")).ShouldNotBeNull();
        exhausted.Quota.ShouldBe("checkpoint-rate/tenant");
    }

    [TestMethod]
    public async Task A_disabled_limit_enforces_nothing()
    {
        Fixture fixture = Fixture.With(o =>
        {
            o.TenantCheckpoints = RunnerQuotaLimit.None;
            o.RunnerCheckpoints = RunnerQuotaLimit.None;
        });

        for (int i = 0; i < 1000; ++i)
        {
            (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        }
    }

    [TestMethod]
    public async Task Volume_is_charged_separately_from_request_count()
    {
        // Request count and volume are different resources. One large save and many small ones exhaust different
        // things, and a single counter would refuse whichever it happened to be tuned for.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(1000, 1000);
            o.RunnerCheckpointBytes = new RunnerQuotaLimit(1024, 1024);
            o.TenantCheckpointBytes = RunnerQuotaLimit.None;
        });

        (await fixture.Guard.TryAcquireAsync(RunnerQuotaKind.CheckpointBytes, Tenant, Runner, 1024, default)).ShouldBeNull();

        RunnerQuotaRejection refused = (await fixture.Guard.TryAcquireAsync(RunnerQuotaKind.CheckpointBytes, Tenant, Runner, 1, default)).ShouldNotBeNull();
        refused.Quota.ShouldBe("checkpoint-bytes/runner");

        // The request-count quota is untouched by the volume refusal.
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_zero_cost_charge_is_free()
    {
        // A zero-byte body is still a request, and the request is charged under its own dimension. Charging nothing for
        // the volume keeps an empty save from being double-charged.
        Fixture fixture = Fixture.With(o => o.RunnerCheckpointBytes = new RunnerQuotaLimit(1, 1));

        for (int i = 0; i < 100; ++i)
        {
            (await fixture.Guard.TryAcquireAsync(RunnerQuotaKind.CheckpointBytes, Tenant, Runner, 0, default)).ShouldBeNull();
        }
    }

    [TestMethod]
    public async Task Counters_are_kept_apart_by_dimension()
    {
        // Exhausting claims must not refuse checkpoints. A runner that cannot take new work should still be able to
        // finish the run it holds.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerClaims = new RunnerQuotaLimit(1, 1);
            o.RunnerCheckpoints = new RunnerQuotaLimit(10, 10);
        });

        await fixture.Guard.TryAcquireAsync(RunnerQuotaKind.Claim, Tenant, Runner, 1, default);
        (await fixture.Guard.TryAcquireAsync(RunnerQuotaKind.Claim, Tenant, Runner, 1, default)).ShouldNotBeNull();

        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Two_tenants_do_not_share_a_counter()
    {
        Fixture fixture = Fixture.With(o =>
        {
            o.TenantCheckpoints = new RunnerQuotaLimit(1, 1);
            o.RunnerCheckpoints = RunnerQuotaLimit.None;
        });

        await fixture.CheckpointAsync(Runner, tenant: "acme");
        (await fixture.CheckpointAsync(Runner, tenant: "acme")).ShouldNotBeNull();

        (await fixture.CheckpointAsync(Runner, tenant: "zeus")).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_retry_after_covers_the_deficit()
    {
        // A Retry-After that is too short is a spin: the caller comes back, is refused again, and has learned nothing.
        // Waiting exactly what it says must be enough.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(2, 2);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        await fixture.CheckpointAsync(Runner);
        await fixture.CheckpointAsync(Runner);
        RunnerQuotaRejection refused = (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        fixture.Clock.Advance(refused.RetryAfter);
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_retry_after_is_never_below_a_second()
    {
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(1000, 1);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        await fixture.CheckpointAsync(Runner);
        RunnerQuotaRejection refused = (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        refused.RetryAfter.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task A_burst_below_the_sustained_rate_is_honoured_and_still_admits_the_rate()
    {
        // Both halves matter. A configured burst is never silently raised, or the setting reads as effective and is
        // not. And capping the burst does not cap throughput: tokens refill continuously, so traffic spread evenly at
        // the sustained rate finds a token waiting however small the burst is.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(5, 1);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        // Burst of one: the second simultaneous request is refused.
        (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();

        // Yet five per second is still admitted when spread across the second.
        for (int i = 0; i < 5; ++i)
        {
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(200));
            (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        }
    }

    [TestMethod]
    public async Task An_unset_burst_defaults_to_a_seconds_worth()
    {
        // Only an unset burst is defaulted. Zero would otherwise refuse everything while the rate says otherwise.
        Fixture fixture = Fixture.With(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(3, 0);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        for (int i = 0; i < 3; ++i)
        {
            (await fixture.CheckpointAsync(Runner)).ShouldBeNull();
        }

        (await fixture.CheckpointAsync(Runner)).ShouldNotBeNull();
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override long TimestampFrequency => 1_000_000;

        public override DateTimeOffset GetUtcNow() => this.now;

        public override long GetTimestamp() => this.now.ToUnixTimeMilliseconds() * 1000;

        public void Advance(TimeSpan by) => this.now += by;
    }

    private sealed class Fixture
    {
        private Fixture(TokenBucketRunnerQuotaGuard guard, TestClock clock)
        {
            this.Guard = guard;
            this.Clock = clock;
        }

        public TokenBucketRunnerQuotaGuard Guard { get; }

        public TestClock Clock { get; }

        public static Fixture With(Action<RunnerQuotaOptions> configure)
        {
            var options = new RunnerQuotaOptions();
            configure(options);

            var clock = new TestClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            return new Fixture(new TokenBucketRunnerQuotaGuard(options, clock), clock);
        }

        public ValueTask<RunnerQuotaRejection?> CheckpointAsync(string principal, string? tenant = Tenant)
            => this.Guard.TryAcquireAsync(RunnerQuotaKind.Checkpoint, tenant, principal, 1, default);
    }
}