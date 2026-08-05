// <copyright file="RunnerAuthorizationBindingsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Coverage of the store-backed binding resolution (ADR 0027, ADR 0065 decision 2): which environments a machine
/// principal may execute runs for, how quickly a revocation takes effect, and the refusal of a principal that holds
/// both a platform and a tenant binding.
/// </summary>
[TestClass]
public sealed class RunnerAuthorizationBindingsTests
{
    private const string Machine = "machine-a";
    private const string Production = "production";
    private const string Staging = "staging";
    private const string Platform = "platform";
    private const string AcmeOne = "acme-one";
    private const string AcmeTwo = "acme-two";
    private const string ZeusOne = "zeus-one";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task An_authorized_principal_resolves_the_environments_it_serves()
    {
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        await fixture.AuthorizeAsync(Staging, "runner-2", Machine);

        RunnerBindings resolved = await fixture.Bindings.ResolveAsync(Machine, default);

        resolved.Environments.ShouldBe([Production, Staging], ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_principal_awaiting_a_decision_is_bound_to_nothing()
    {
        // Registering is not authorization: a runner that has asked to serve an environment is offered no work until an
        // administrator of that environment decides.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.RegisterAsync(Production, "runner-1", Machine);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Another_principals_authorization_binds_nothing_here()
    {
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", "machine-b");

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_quarantined_or_revoked_runner_is_bound_to_nothing()
    {
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        await fixture.DecideAsync(Production, "runner-1", RunnerAuthorizationStatus.Quarantined);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();

        await fixture.DecideAsync(Production, "runner-1", RunnerAuthorizationStatus.Revoked);
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_revocation_takes_effect_within_the_cache_window_and_not_before()
    {
        // The cache window IS the revocation fence's latency, which is why it is bounded rather than tuned. This test is
        // the property: stale until the window elapses, gone immediately after.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBe([Production]);

        await fixture.DecideAsync(Production, "runner-1", RunnerAuthorizationStatus.Revoked);

        fixture.Clock.Advance(TimeSpan.FromSeconds(29));
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBe([Production]);

        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_cache_window_longer_than_the_bound_is_reduced_to_it()
    {
        // A deployment does not get to choose a slower fence: asking for an hour gets thirty seconds.
        Fixture fixture = await Fixture.StartAsync(TimeSpan.FromHours(1));
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBe([Production]);

        await fixture.DecideAsync(Production, "runner-1", RunnerAuthorizationStatus.Revoked);

        fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_zero_window_resolves_every_time()
    {
        Fixture fixture = await Fixture.StartAsync(TimeSpan.Zero);
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBe([Production]);

        await fixture.DecideAsync(Production, "runner-1", RunnerAuthorizationStatus.Revoked);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_principal_holding_both_a_platform_and_a_tenant_binding_is_bound_to_nothing()
    {
        // ADR 0065 decision 2: holding both is a laundering route out of a sealed environment into the never-sealed
        // platform one. It resolves to nothing at all — picking a side would leave the route open whichever was kept.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);
        await fixture.AuthorizeAsync(Platform, "runner-2", Machine);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_principal_bound_only_to_the_platform_environment_serves_it()
    {
        // The platform's own runners are not the thing being fenced; only the combination is.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Platform, "runner-1", Machine);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBe([Platform]);
    }

    [TestMethod]
    public async Task A_binding_whose_environment_no_longer_exists_is_dropped()
    {
        // The authorization outlived its environment. A runner cannot serve one that is not there, and treating the
        // binding as live would be trusting a name nothing backs.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync("deleted-environment", "runner-1", Machine);

        (await fixture.Bindings.ResolveAsync(Machine, default)).Environments.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_tenant_is_the_owner_group_of_the_bound_environments()
    {
        // The counter a per-tenant quota is measured against (ADR 0065 decision 3). It comes from the same read and the
        // same cache window as the environments, so a revoked runner stops being charged when it stops being offered
        // work rather than on some other schedule.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(AcmeOne, "runner-1", Machine);
        await fixture.AuthorizeAsync(AcmeTwo, "runner-2", Machine);

        RunnerBindings resolved = await fixture.Bindings.ResolveAsync(Machine, default);

        resolved.Environments.ShouldBe([AcmeOne, AcmeTwo], ignoreOrder: true);
        resolved.Tenant.ShouldBe("acme");
    }

    [TestMethod]
    public async Task An_environment_carrying_no_owner_group_resolves_no_tenant()
    {
        // A deployment that publishes nothing to tell owner groups apart has exactly one tenant by construction, so its
        // usage counts against the deployment rather than against a named group. Not an error.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(Production, "runner-1", Machine);

        RunnerBindings resolved = await fixture.Bindings.ResolveAsync(Machine, default);

        resolved.Environments.ShouldBe([Production]);
        resolved.Tenant.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_principal_bound_across_two_owner_groups_is_bound_to_nothing()
    {
        // The same argument as the platform/tenant pair: a binding spanning owner groups is a cross-tenant handle, which
        // is exactly what the tenancy invariant exists to prevent. It also leaves no unambiguous answer to which tenant
        // the usage is charged, and picking one would make the answer depend on binding order — a quota counter a caller
        // could steer by arranging its own authorizations.
        Fixture fixture = await Fixture.StartAsync();
        await fixture.AuthorizeAsync(AcmeOne, "runner-1", Machine);
        await fixture.AuthorizeAsync(ZeusOne, "runner-2", Machine);

        RunnerBindings resolved = await fixture.Bindings.ResolveAsync(Machine, default);

        resolved.Environments.ShouldBeEmpty();
        resolved.Tenant.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_prefix_that_disagrees_with_the_stamp_resolves_no_tenant()
    {
        // The silent failure the shared derivation exists to prevent, asserted at this layer too: the reach is resolved
        // correctly and the counter is not, so every tenant would share one bucket while nothing reports a problem.
        Fixture fixture = await Fixture.StartAsync(internalTagPrefix: "corp:");
        await fixture.AuthorizeAsync(AcmeOne, "runner-1", Machine);

        RunnerBindings resolved = await fixture.Bindings.ResolveAsync(Machine, default);

        resolved.Environments.ShouldBe([AcmeOne]);
        resolved.Tenant.ShouldBeNull();
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }

    private sealed class Fixture
    {
        private Fixture(InMemoryEnvironmentRunnerAuthorizationStore authorizations, TestClock clock, RunnerAuthorizationBindings bindings)
        {
            this.Authorizations = authorizations;
            this.Clock = clock;
            this.Bindings = bindings;
        }

        public InMemoryEnvironmentRunnerAuthorizationStore Authorizations { get; }

        public TestClock Clock { get; }

        public RunnerAuthorizationBindings Bindings { get; }

        public static async ValueTask<Fixture> StartAsync(TimeSpan? cacheWindow = null, string? internalTagPrefix = null)
        {
            var clock = new TestClock(T0);
            var authorizations = new InMemoryEnvironmentRunnerAuthorizationStore(clock);
            var environments = new InMemoryEnvironmentStore(clock);

            // Two tenant environments and the control plane's own, which is marked rather than named: a name is chosen
            // by the party the exclusion defends against. None of the three carries an owner group, which is the shape
            // of a deployment that publishes nothing to tell tenancies apart.
            await AddAsync(environments, Environments.Environment.Draft(Production, "Production", null, default));
            await AddAsync(environments, Environments.Environment.Draft(Staging, "Staging", null, default));
            await AddAsync(environments, Environments.Environment.DraftPlatform(Platform, "Platform", null, default));

            // And three that do, across two owner groups.
            await AddAsync(environments, Owned(AcmeOne, "acme"));
            await AddAsync(environments, Owned(AcmeTwo, "acme"));
            await AddAsync(environments, Owned(ZeusOne, "zeus"));

            var bindings = new RunnerAuthorizationBindings(authorizations, environments, cacheWindow, timeProvider: clock, internalTagPrefix: internalTagPrefix);
            return new Fixture(authorizations, clock, bindings);
        }

        private static ParsedJsonDocument<Environments.Environment> Owned(string name, string ownerGroup)
            => Environments.Environment.Draft(
                name,
                null,
                null,
                SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + OwnerGroupTag.Dimension, ownerGroup)]));

        private static async ValueTask AddAsync(IEnvironmentStore environments, ParsedJsonDocument<Environments.Environment> draft)
        {
            using (draft)
            using (await environments.AddAsync(draft.RootElement, "ops", default))
            {
            }
        }

        public async ValueTask RegisterAsync(string environment, string runnerId, string principal)
        {
            using (await this.Authorizations.EnsurePendingAsync(environment, runnerId, runnerId, principal, default))
            {
            }
        }

        public async ValueTask AuthorizeAsync(string environment, string runnerId, string principal)
        {
            await this.RegisterAsync(environment, runnerId, principal);
            await this.DecideAsync(environment, runnerId, RunnerAuthorizationStatus.Authorized);
        }

        public async ValueTask DecideAsync(string environment, string runnerId, RunnerAuthorizationStatus status)
        {
            using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await this.Authorizations.DecideAsync(
                environment,
                runnerId,
                new RunnerAuthorizationDecision(status),
                WorkflowEtag.None,
                "admin",
                default);
        }
    }
}