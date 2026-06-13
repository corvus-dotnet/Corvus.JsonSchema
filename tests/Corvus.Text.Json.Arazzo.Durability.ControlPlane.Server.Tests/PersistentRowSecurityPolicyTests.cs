// <copyright file="PersistentRowSecurityPolicyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="PersistentRowSecurityPolicy"/> (§14.2): resolving a principal's <see cref="AccessContext"/>
/// from persisted rules + claim→rule bindings — per-verb grants, OR across matched bindings, the Unrestricted
/// operator escape, deny-by-default for an unmatched principal, and the shell wrapper.
/// </summary>
[TestClass]
public sealed class PersistentRowSecurityPolicyTests
{
    private static readonly SecurityTag[] Acme = [new("tenant", "acme")];
    private static readonly SecurityTag[] Globex = [new("tenant", "globex")];

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
        ctx.Admits(AccessVerb.Read, []).ShouldBeTrue(); // untagged visible only to full reach
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
        await store.AddRuleAsync("acme-only", new SecurityRuleDefinition("tenant == 'acme'"), "admin", default);
        await store.AddRuleAsync("globex-only", new SecurityRuleDefinition("tenant == 'globex'"), "admin", default);

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
        await store.AddRuleAsync("team-payments", new SecurityRuleDefinition("team == 'payments'"), "admin", default);
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "u", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None), "admin", default);

        // Deployment shell mandates the tenant via an internal tag; the binding narrows by team.
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        var policy = new PersistentRowSecurityPolicy(store, shell);
        await policy.RefreshAsync();

        AccessContext ctx = policy.Resolve(Principal(("role", "u"), ("tenant", "acme")));
        ctx.Admits(AccessVerb.Read, [new("sys:tenant", "acme"), new("team", "payments")]).ShouldBeTrue();
        ctx.Admits(AccessVerb.Read, [new("sys:tenant", "globex"), new("team", "payments")]).ShouldBeFalse(); // wrapper fails
        ctx.Admits(AccessVerb.Read, [new("sys:tenant", "acme"), new("team", "hr")]).ShouldBeFalse(); // binding fails
    }
}
