// <copyright file="KeycloakClaimsTransformerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo.Tests;

/// <summary>
/// Proves the demo's §16.5.2 Decision-A claim→capability mapping: effective scopes are the read baseline a group
/// confers <b>unioned with the principal's stored per-principal grants</b> (<c>claims ∪ stored entitlements</c>), and
/// no group — not even <c>arazzo-admins</c> — confers standing elevated capability (§16.5.3, no ambient god-mode).
/// This is the worked example the demo README walks through, exercised against the real
/// <see cref="PersistentRowSecurityPolicy"/> resolver the durable server uses.
/// </summary>
[TestClass]
public sealed class KeycloakClaimsTransformerTests
{
    // The standing read baseline every group member holds (mirrors KeycloakClaimsTransformer.DomainReadScopes).
    private static readonly string[] ReadBaseline =
    [
        ControlPlaneScopes.CatalogRead,
        ControlPlaneScopes.RunsRead,
        ControlPlaneScopes.CredentialsRead,
        ControlPlaneScopes.AdministratorsRead,
        ControlPlaneScopes.SecurityRead,
    ];

    /// <summary>
    /// A domain user (alice, group <c>payments</c>) holding a stored <c>runs:write</c> grant — the durable shape an
    /// approval/self-elevation writes — gets that grant unioned into her effective scopes on top of the read baseline.
    /// </summary>
    [TestMethod]
    public async Task StoredGrantUnionsWithReadBaseline()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);

        // The per-principal grant an approval/self-elevation persists: keyed on the subject claim, granting a scope.
        using (ParsedJsonDocument<SecurityBindingDocument> grant = SecurityBindingDocument.Draft("preferred_username", "alice", VerbGrant.None, VerbGrant.None, VerbGrant.None, scopes: [ControlPlaneScopes.RunsWrite]))
        {
            (await store.AddBindingAsync(grant.RootElement, "approver", default)).Dispose();
        }

        var entitlements = new PersistentRowSecurityPolicy(store);
        await entitlements.RefreshAsync();
        var transformer = new KeycloakClaimsTransformer(entitlements);

        ClaimsPrincipal alice = Principal(("preferred_username", "alice"), ("groups", "payments"));
        ClaimsPrincipal mapped = await transformer.TransformAsync(alice);

        HashSet<string> scopes = Scopes(mapped);
        scopes.ShouldBe([.. ReadBaseline, ControlPlaneScopes.RunsWrite], ignoreOrder: true);
    }

    /// <summary>
    /// An <c>arazzo-admins</c> member with no stored grant gets only the standing read baseline — being in the
    /// administrator group confers no <c>runs:write</c> / <c>security:write</c> ambient capability (§16.5.3).
    /// </summary>
    [TestMethod]
    public async Task AdminGroupConfersReadOnlyNoAmbientWrite()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);

        var entitlements = new PersistentRowSecurityPolicy(store);
        await entitlements.RefreshAsync();
        var transformer = new KeycloakClaimsTransformer(entitlements);

        ClaimsPrincipal boss = Principal(("preferred_username", "boss"), ("groups", "arazzo-admins"));
        ClaimsPrincipal mapped = await transformer.TransformAsync(boss);

        HashSet<string> scopes = Scopes(mapped);
        scopes.ShouldBe(ReadBaseline, ignoreOrder: true);
        scopes.ShouldNotContain(ControlPlaneScopes.RunsWrite);
        scopes.ShouldNotContain(ControlPlaneScopes.SecurityWrite);
    }

    /// <summary>
    /// §16.2 tier 3 — the genesis administrator. With the deployment-policy grant seeded (the <c>arazzo-admins</c>
    /// group claim mapped to all capability scopes + unrestricted reach — exactly the binding the demo host seeds),
    /// an <c>arazzo-admins</c> member's effective scopes ARE the full service-operator set. This is a STORED grant
    /// (config-as-code), so it does not contradict <see cref="AdminGroupConfersReadOnlyNoAmbientWrite"/>: membership
    /// alone still confers nothing; the deliberate, auditable deployment binding is what confers it — the identity
    /// analogue of secret-zero, bootstrapped by config rather than an in-system grant.
    /// </summary>
    [TestMethod]
    public async Task GenesisAdminGrantConfersAllScopes()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);

        using (ParsedJsonDocument<SecurityBindingDocument> tier3 = SecurityBindingDocument.Draft("groups", "arazzo-admins", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full, scopes: ControlPlaneScopes.All))
        {
            (await store.AddBindingAsync(tier3.RootElement, "bootstrap", default)).Dispose();
        }

        var entitlements = new PersistentRowSecurityPolicy(store);
        await entitlements.RefreshAsync();
        var transformer = new KeycloakClaimsTransformer(entitlements);

        ClaimsPrincipal admin = Principal(("preferred_username", "arazzo-admin"), ("groups", "arazzo-admins"));
        ClaimsPrincipal mapped = await transformer.TransformAsync(admin);

        HashSet<string> scopes = Scopes(mapped);
        scopes.ShouldBe([.. ControlPlaneScopes.All], ignoreOrder: true);
        scopes.ShouldContain(ControlPlaneScopes.RunsWrite);
        scopes.ShouldContain(ControlPlaneScopes.SecurityWrite);
    }

    /// <summary>A principal carrying no <c>groups</c> claim (e.g. a DevApiKey principal) is left untouched.</summary>
    [TestMethod]
    public async Task PrincipalWithoutGroupsIsUntouched()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);
        var entitlements = new PersistentRowSecurityPolicy(store);
        await entitlements.RefreshAsync();
        var transformer = new KeycloakClaimsTransformer(entitlements);

        ClaimsPrincipal devApiKey = Principal(("sub", "dev"), ("scope", "catalog:read runs:read"));
        ClaimsPrincipal mapped = await transformer.TransformAsync(devApiKey);

        // No `scope` claim was added by the transformer — the principal keeps exactly the one it arrived with.
        mapped.FindAll("scope").Count().ShouldBe(1);
        mapped.FindFirst("scope")!.Value.ShouldBe("catalog:read runs:read");
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestAuth"));

    private static HashSet<string> Scopes(ClaimsPrincipal principal)
        => principal.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.Ordinal);
}