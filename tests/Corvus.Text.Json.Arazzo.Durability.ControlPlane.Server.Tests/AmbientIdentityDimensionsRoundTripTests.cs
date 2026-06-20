// <copyright file="AmbientIdentityDimensionsRoundTripTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The regression lock for ambient identity dimensions (design §16.5.5): a grantee resolved within a request's tenant
/// context — whether via the non-directory <see cref="ControlPlaneRowSecurityPolicy.ResolveGranteeIdentity"/> or the
/// directory <see cref="DirectoryPrincipalProjector"/> — set-equals the runtime caller in that same context (membership
/// and reach), and does <strong>not</strong> match a caller in a different context. This is the test that catches the
/// silent-inert trap §16.5.5 exists to prevent: identity is stamped from one provider at both moments, so the two can
/// never drift. The host context is the request's vanity host, mapped to its tenant by an authoritative allow-list.
/// </summary>
[TestClass]
public sealed class AmbientIdentityDimensionsRoundTripTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> Hosts = new Dictionary<string, IReadOnlyList<SecurityTag>>
    {
        ["acme.example"] = [new SecurityTag("sys:tenant", "acme")],
        ["globex.example"] = [new SecurityTag("sys:tenant", "globex")],
    };

    [TestMethod]
    public async Task A_grantee_resolved_in_a_tenant_context_matches_the_caller_in_that_context()
    {
        (HttpContextAccessor accessor, HttpRequestAmbientIdentityDimensions provider) = HostProvider();
        var store = new InMemorySecurityPolicyStore();
        var policy = new PersistentRowSecurityPolicy(store, internalTagResolver: SubTags, ambient: provider);
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"));

        // Authoring: resolve the grantee within the acme context — it carries sys:tenant=acme by construction.
        SetHost(accessor, "acme.example");
        SecurityTagSet granteeAcme = policy.ResolveGranteeIdentity(GranteeKind.Person, "alice");
        granteeAcme.ToList().ShouldBe([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "acme")], ignoreOrder: true);

        // Runtime: the same caller in the acme context stamps the same identity → membership matches.
        SetHost(accessor, "acme.example");
        SecurityTagSet callerAcme = SecurityTagSet.FromTags(policy.GetInternalTags(alice));
        WorkflowIdentity.SameAdministrator(granteeAcme, callerAcme).ShouldBeTrue();

        // The same caller in a DIFFERENT tenant context does NOT match the acme grant (the silent-inert trap avoided).
        SetHost(accessor, "globex.example");
        SecurityTagSet callerGlobex = SecurityTagSet.FromTags(policy.GetInternalTags(alice));
        WorkflowIdentity.SameAdministrator(granteeAcme, callerGlobex).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_directory_resolved_grantee_matches_the_caller_in_the_same_context()
    {
        (HttpContextAccessor accessor, HttpRequestAmbientIdentityDimensions provider) = HostProvider();
        var store = new InMemorySecurityPolicyStore();

        // A deployment whose runtime identity mirrors its directory issuer (the DirectoryIssuer runtime contract) — both
        // sides stamp sys:iss=kc, and both take sys:tenant from the SAME ambient provider.
        var policy = new PersistentRowSecurityPolicy(store, internalTagResolver: SubAndIssTags, ambient: provider);
        await policy.RefreshAsync();

        var projector = new DirectoryPrincipalProjector(
            DirectoryIdentityMapper.FromFunc(static record => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)]))),
            "kc",
            provider);

        ClaimsPrincipal alice = Principal(("sub", "alice"));

        // Authoring via the directory projector, in the acme context.
        SetHost(accessor, "acme.example");
        SecurityTagSet granteeAcme = projector.Project(new DirectoryRecord(GranteeKind.Person, "alice", "Alice", new Dictionary<string, IReadOnlyList<string>>(), []))!.Value.Identity;
        granteeAcme.ToList().ShouldBe([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:iss", "kc"), new SecurityTag("sys:tenant", "acme")], ignoreOrder: true);

        // The runtime caller in the acme context stamps the identical identity → membership matches; globex does not.
        SetHost(accessor, "acme.example");
        WorkflowIdentity.SameAdministrator(granteeAcme, SecurityTagSet.FromTags(policy.GetInternalTags(alice))).ShouldBeTrue();

        SetHost(accessor, "globex.example");
        WorkflowIdentity.SameAdministrator(granteeAcme, SecurityTagSet.FromTags(policy.GetInternalTags(alice))).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Reach_follows_the_request_context_tenant()
    {
        (HttpContextAccessor accessor, HttpRequestAmbientIdentityDimensions provider) = HostProvider();
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "team-payments", "team == 'payments'", "admin");
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "member", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None), "admin", default);

        // The deployment shell mandates the tenant from the caller's context: sys:tenant == $claim.tenant.
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        var policy = new PersistentRowSecurityPolicy(store, shell, internalTagResolver: SubTags, ambient: provider);
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"), ("role", "member"));

        SetHost(accessor, "acme.example");
        AccessContext acme = policy.Resolve(alice);
        acme.Admits(AccessVerb.Read, Row("acme", "payments")).ShouldBeTrue();
        acme.Admits(AccessVerb.Read, Row("globex", "payments")).ShouldBeFalse(); // wrapper: row tenant != the context tenant

        SetHost(accessor, "globex.example");
        AccessContext globex = policy.Resolve(alice);
        globex.Admits(AccessVerb.Read, Row("globex", "payments")).ShouldBeTrue();
        globex.Admits(AccessVerb.Read, Row("acme", "payments")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_unresolved_context_denies_tenant_scoped_reach_fail_closed()
    {
        (HttpContextAccessor accessor, HttpRequestAmbientIdentityDimensions provider) = HostProvider();
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "team-payments", "team == 'payments'", "admin");
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "member", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None), "admin", default);
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        var policy = new PersistentRowSecurityPolicy(store, shell, internalTagResolver: SubTags, ambient: provider);
        await policy.RefreshAsync();

        ClaimsPrincipal alice = Principal(("sub", "alice"), ("role", "member"));

        // An unknown host is not in the allow-list → no tenant claim is injected → the wrapper's $claim.tenant is the
        // empty set → no tenant row is admitted (fail-closed, never a blank match that crosses tenants).
        SetHost(accessor, "unknown.example");
        AccessContext ctx = policy.Resolve(alice);
        ctx.Admits(AccessVerb.Read, Row("acme", "payments")).ShouldBeFalse();
        ctx.Admits(AccessVerb.Read, Row("globex", "payments")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_token_supplied_tenant_claim_cannot_widen_the_context_reach()
    {
        (HttpContextAccessor accessor, HttpRequestAmbientIdentityDimensions provider) = HostProvider();
        var store = new InMemorySecurityPolicyStore();
        await SeedRuleAsync(store, "team-payments", "team == 'payments'", "admin");
        await store.AddBindingAsync(new SecurityBindingDefinition("role", "member", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None), "admin", default);
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        var policy = new PersistentRowSecurityPolicy(store, shell, internalTagResolver: SubTags, ambient: provider);
        await policy.RefreshAsync();

        // The attacker is in the acme context but forges a token tenant=globex claim, trying to reach globex rows.
        ClaimsPrincipal attacker = Principal(("sub", "mallory"), ("role", "member"), ("tenant", "globex"));

        SetHost(accessor, "acme.example");
        AccessContext ctx = policy.Resolve(attacker);

        // Ambient is authoritative: the context tenant (acme) replaces the forged claim → globex stays denied.
        ctx.Admits(AccessVerb.Read, Row("globex", "payments")).ShouldBeFalse();
        ctx.Admits(AccessVerb.Read, Row("acme", "payments")).ShouldBeTrue();
    }

    private static (HttpContextAccessor Accessor, HttpRequestAmbientIdentityDimensions Provider) HostProvider()
    {
        var accessor = new HttpContextAccessor();
        return (accessor, HttpRequestAmbientIdentityDimensions.ByHost(accessor, Hosts));
    }

    private static void SetHost(HttpContextAccessor accessor, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        accessor.HttpContext = context;
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).ToList(), "test"));

    // A deployment hook mapping the principal's sub claim to its sys:sub internal tag (the caller's identity for row
    // creation + administration membership).
    private static IReadOnlyList<SecurityTag> SubTags(ClaimsPrincipal? principal)
        => principal?.FindFirst("sub") is { } sub ? [new SecurityTag("sys:sub", sub.Value)] : [];

    // The same, plus the deployment's issuer (sys:iss=kc) — mirroring a directory-resolved identity so the runtime
    // caller set-equals a directory-projected grantee (the DirectoryIssuer runtime contract).
    private static IReadOnlyList<SecurityTag> SubAndIssTags(ClaimsPrincipal? principal)
        => principal?.FindFirst("sub") is { } sub ? [new SecurityTag("sys:sub", sub.Value), new SecurityTag("sys:iss", "kc")] : [];

    private static SecurityTagSet Row(string tenant, string team)
        => SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant), new SecurityTag("team", team)]);

    // Seeds a rule from a pooled, disposable draft (the store reads it synchronously), disposing both the draft and the
    // created document — the test asserts on resolution, not the seed record.
    private static async Task SeedRuleAsync(InMemorySecurityPolicyStore store, string name, string expression, string actor)
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(expression);
        (await store.AddRuleAsync(name, draft.RootElement, actor, default)).Dispose();
    }
}