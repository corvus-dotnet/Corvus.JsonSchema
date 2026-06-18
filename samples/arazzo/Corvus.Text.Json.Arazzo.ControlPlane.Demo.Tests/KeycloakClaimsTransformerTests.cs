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
        await store.AddBindingAsync(
            new SecurityBindingDefinition("preferred_username", "alice", VerbGrant.None, VerbGrant.None, VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite]),
            "approver",
            default);

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