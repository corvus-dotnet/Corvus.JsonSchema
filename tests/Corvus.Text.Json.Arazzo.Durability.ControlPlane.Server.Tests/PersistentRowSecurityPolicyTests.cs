// <copyright file="PersistentRowSecurityPolicyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="PersistentRowSecurityPolicy"/> (§14.2): resolving a principal's <see cref="AccessContext"/>
/// from persisted rules + claim→rule bindings — per-verb grants, OR across matched bindings, the Unrestricted
/// operator escape, deny-by-default for an unmatched principal, and the shell wrapper.
/// </summary>
[TestClass]
public sealed class PersistentRowSecurityPolicyTests
{
    private static readonly SecurityTagSet Acme = SecurityTagSet.FromTags([new("tenant", "acme")]);
    private static readonly SecurityTagSet Globex = SecurityTagSet.FromTags([new("tenant", "globex")]);

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).ToList(), "test"));

    [TestMethod]
    public async Task Before_refresh_everything_is_denied()
    {
        var store = new InMemorySecurityPolicyStore();
        var policy = new PersistentRowSecurityPolicy(store);

        // No RefreshAsync yet → no bindings → deny-by-default for every verb.
        AccessContext ctx = policy.Resolve(Principal(("role", "operator")));
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeFalse();
        ctx.Admits(AccessVerb.Write, Acme).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_tenant_binding_scopes_read_and_write_and_denies_ungranted_purge()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);
        await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "tenant-admin", VerbGrant.Rules("tenant-scoped"), VerbGrant.Rules("tenant-scoped"), VerbGrant.None),
            "admin",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "tenant-admin"), ("tenant", "acme")));
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeTrue();
        ctx.Admits(AccessVerb.Read, Globex).ShouldBeFalse();
        ctx.Admits(AccessVerb.Write, Acme).ShouldBeTrue();

        // No purge grant → empty filter → deny (even for the principal's own tenant).
        ctx.Admits(AccessVerb.Purge, Acme).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_unrestricted_binding_grants_full_reach_including_untagged_rows()
    {
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full),
            "admin",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "operator")));
        ctx.Reach(AccessVerb.Read).ShouldBeNull(); // unrestricted
        ctx.Admits(AccessVerb.Read, Globex).ShouldBeTrue();
        ctx.Admits(AccessVerb.Read, SecurityTagSet.Empty).ShouldBeTrue(); // untagged visible only to full reach
    }

    [TestMethod]
    public async Task An_unmatched_authenticated_principal_is_denied_everything()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);
        await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "tenant-admin", VerbGrant.Rules("tenant-scoped"), VerbGrant.None, VerbGrant.None),
            "admin",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "nobody"), ("tenant", "acme")));
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Grants_compose_as_or_across_matched_bindings()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "acme-only", "tenant == 'acme'", "admin");
        await SeedRuleAsync(store, "globex-only", "tenant == 'globex'", "admin");

        // Two bindings the principal matches (by two different claims), each granting a different tenant for read.
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "a", VerbGrant.Rules("acme-only"), VerbGrant.None, VerbGrant.None), "admin", default);
        await store.AddBindingAsync(new SecurityBindingDefinition("group", "g", VerbGrant.Rules("globex-only"), VerbGrant.None, VerbGrant.None), "admin", default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "a"), ("group", "g")));
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeTrue();   // from acme-only
        ctx.Admits(AccessVerb.Read, Globex).ShouldBeTrue(); // from globex-only (OR)
    }

    [TestMethod]
    public async Task A_null_or_unauthenticated_principal_is_denied()
    {
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "admin", default);
        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        policy.Resolve(null).Admits(AccessVerb.Read, Acme).ShouldBeFalse();
        policy.Resolve(new ClaimsPrincipal(new ClaimsIdentity())).Admits(AccessVerb.Read, Acme).ShouldBeFalse(); // not authenticated
    }

    [TestMethod]
    public async Task The_shell_wrapper_is_anded_into_every_grant()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "team-payments", "team == 'payments'", "admin");
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "u", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None), "admin", default);

        // Deployment shell mandates the tenant via an internal tag; the binding narrows by team.
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        var policy = new PersistentRowSecurityPolicy(store, shell);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "u"), ("tenant", "acme")));
        ctx.Admits(AccessVerb.Read, SecurityTagSet.FromTags([new("sys:tenant", "acme"), new("team", "payments")])).ShouldBeTrue();
        ctx.Admits(AccessVerb.Read, SecurityTagSet.FromTags([new("sys:tenant", "globex"), new("team", "payments")])).ShouldBeFalse(); // wrapper fails
        ctx.Admits(AccessVerb.Read, SecurityTagSet.FromTags([new("sys:tenant", "acme"), new("team", "hr")])).ShouldBeFalse(); // binding fails
    }

    [TestMethod]
    public async Task A_binding_grants_capability_scopes_to_the_matched_principal()
    {
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.None, VerbGrant.None, VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite, ControlPlaneScopes.RunsRead]),
            "approver",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        // The granted scopes survive the store round-trip (schema → write → snapshot → resolve).
        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBe([ControlPlaneScopes.RunsWrite, ControlPlaneScopes.RunsRead], ignoreOrder: true);

        // A different principal is granted nothing.
        policy.ResolveGrantedScopes(Principal(("sub", "bob"))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_reach_only_binding_grants_no_capability_scopes()
    {
        var store = new InMemorySecurityPolicyStore();

        // A binding with reach grants but no scopes is the common (standing-rule) case → the zero-allocation fast path.
        await store.AddBindingAsync(new SecurityBindingDefinition("sub", "alice", VerbGrant.Full, VerbGrant.None, VerbGrant.None), "admin", default);
        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        policy.ResolveGrantedScopes(Principal(("sub", "alice"))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Granted_scopes_union_and_deduplicate_across_matched_bindings()
    {
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(new SecurityBindingDefinition("sub", "alice", VerbGrant.None, VerbGrant.None, VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite]), "approver", default);
        await store.AddBindingAsync(new SecurityBindingDefinition("team", "payments", VerbGrant.None, VerbGrant.None, VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite, ControlPlaneScopes.CatalogRead]), "approver", default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        // runs:write appears in both bindings but resolves once (union, deduplicated).
        policy.ResolveGrantedScopes(Principal(("sub", "alice"), ("team", "payments")))
            .ShouldBe([ControlPlaneScopes.RunsWrite, ControlPlaneScopes.CatalogRead], ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_scope_grant_does_not_alter_the_bindings_reach()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "payments-domain", "domain == 'payments'", "admin");

        // A single entitlement carrying BOTH a capability (runs:write) AND a reach (domain=payments) — the §16.5 shape.
        await store.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.Rules("payments-domain"), VerbGrant.Rules("payments-domain"), VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite]),
            "approver",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"));
        policy.ResolveGrantedScopes(alice).ShouldContain(ControlPlaneScopes.RunsWrite);

        // Reach is resolved exactly as before — scoped to the payments domain, denied elsewhere.
        AccessContext ctx = policy.Resolve(alice);
        ctx.Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("domain", "payments")])).ShouldBeTrue();
        ctx.Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("domain", "billing")])).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_null_or_unauthenticated_principal_is_granted_no_scopes()
    {
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.None, VerbGrant.None, VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite]), "approver", default);
        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        policy.ResolveGrantedScopes(null).ShouldBeEmpty();
        policy.ResolveGrantedScopes(new ClaimsPrincipal(new ClaimsIdentity())).ShouldBeEmpty(); // not authenticated
    }

    [TestMethod]
    public async Task An_expired_grant_confers_no_capability_or_reach()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "payments-domain", "domain == 'payments'", "admin");

        // A time-bound grant whose expiry is already in the past relative to the policy's clock (§16.5.2).
        await store.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.Rules("payments-domain"), VerbGrant.Rules("payments-domain"), VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite], ExpiresAt: ClockNow.AddMinutes(-1)),
            "approver",
            default);

        var policy = new PersistentRowSecurityPolicy(store, timeProvider: new FixedClock(ClockNow));
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"));

        // Expired → excluded fail-safe from both capability and reach.
        policy.ResolveGrantedScopes(alice).ShouldBeEmpty();
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("domain", "payments")])).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_future_dated_grant_is_active_until_it_expires()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "payments-domain", "domain == 'payments'", "admin");
        await store.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.Rules("payments-domain"), VerbGrant.Rules("payments-domain"), VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite], ExpiresAt: ClockNow.AddHours(1)),
            "approver",
            default);

        var policy = new PersistentRowSecurityPolicy(store, timeProvider: new FixedClock(ClockNow));
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"));

        // Not yet expired → active (both capability and reach).
        policy.ResolveGrantedScopes(alice).ShouldContain(ControlPlaneScopes.RunsWrite);
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("domain", "payments")])).ShouldBeTrue();
    }

    [TestMethod]
    public async Task An_eligible_only_binding_confers_no_active_capability_or_reach()
    {
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "payments-domain", "domain == 'payments'", "admin");

        // An eligibility assignment (§16.5.3/§16.5.4): it carries a scope + reach, but eligibleOnly means the resolver
        // must ignore it entirely — eligibility confers nothing active (the self-elevation strategy reads it instead).
        await store.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.Rules("payments-domain"), VerbGrant.Rules("payments-domain"), VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite], EligibleOnly: true),
            "approver",
            default);

        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"));
        policy.ResolveGrantedScopes(alice).ShouldBeEmpty(); // no active capability
        policy.Resolve(alice).Admits(AccessVerb.Write, SecurityTagSet.FromTags([new("domain", "payments")])).ShouldBeFalse(); // nor reach
    }

    [TestMethod]
    public async Task A_wildcard_binding_cannot_grant_unrestricted_reach_by_default()
    {
        // §17.5/F7: a `*` binding matches every authenticated principal; an Unrestricted grant on it would make
        // everyone an operator. By default that grant is demoted to no reach (deny-by-default).
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "admin", default);
        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("sub", "anyone")));
        ctx.Reach(AccessVerb.Read).ShouldNotBeNull(); // NOT unrestricted (demoted)
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeFalse(); // demoted to no reach
        ctx.Admits(AccessVerb.Write, Acme).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_wildcard_binding_with_rule_bounded_reach_is_still_honoured()
    {
        // F7 only blocks the Unrestricted form; a `*` binding with explicit rule-bounded reach is the operator's
        // deliberate, scoped choice and still applies to every principal.
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "acme-only", "tenant == 'acme'", "admin");
        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Rules("acme-only"), VerbGrant.None, VerbGrant.None), "admin", default);
        var policy = new PersistentRowSecurityPolicy(store);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("sub", "anyone")));
        ctx.Admits(AccessVerb.Read, Acme).ShouldBeTrue();   // rule-bounded reach honoured
        ctx.Admits(AccessVerb.Read, Globex).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_deployment_can_opt_into_wildcard_unrestricted_reach()
    {
        // A genuinely single-tenant deployment may opt back in to "everyone is an operator".
        var store = new InMemorySecurityPolicyStore();
        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "admin", default);
        var policy = new PersistentRowSecurityPolicy(store, allowWildcardUnrestrictedReach: true);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("sub", "anyone")));
        ctx.Reach(AccessVerb.Read).ShouldBeNull(); // unrestricted, by explicit opt-in
        ctx.Admits(AccessVerb.Read, Globex).ShouldBeTrue();
    }

    private static readonly DateTimeOffset ClockNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    // A clock fixed at a known instant so a binding's expiry is deterministic relative to "now".
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Seeds a rule from a pooled, disposable draft (the store reads it synchronously), disposing both the draft and the
    // created document — the test asserts on resolution, not the seed record.
    private static async Task SeedRuleAsync(InMemorySecurityPolicyStore store, string name, string expression, string actor)
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(expression);
        (await store.AddRuleAsync(name, draft.RootElement, actor, default)).Dispose();
    }
}
