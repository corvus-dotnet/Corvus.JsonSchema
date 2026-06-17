// <copyright file="ControlPlaneEntitlementClaimsTransformerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="ControlPlaneEntitlementClaimsTransformer"/> (§16.5): the policy's per-principal granted
/// capability scopes are unioned into the principal's <c>scope</c> claim at authentication time, so a stored grant
/// reaches the scope-authorization layer — while the common (no-grant) path leaves the principal untouched.
/// </summary>
[TestClass]
public sealed class ControlPlaneEntitlementClaimsTransformerTests
{
    private static IReadOnlyList<string> ScopeClaims(ClaimsPrincipal principal)
        => principal.Claims.Where(c => c.Type == "scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();

    private static ClaimsPrincipal Authenticated(string sub)
        => new(new ClaimsIdentity([new Claim("sub", sub)], "test"));

    [TestMethod]
    public async Task Granted_scopes_are_stamped_into_the_scope_claim()
    {
        var transformer = new ControlPlaneEntitlementClaimsTransformer(new StubPolicy([ControlPlaneScopes.RunsWrite, ControlPlaneScopes.CatalogRead]));

        ClaimsPrincipal result = await transformer.TransformAsync(Authenticated("alice"));

        ScopeClaims(result).ShouldBe([ControlPlaneScopes.RunsWrite, ControlPlaneScopes.CatalogRead], ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_principal_with_no_grants_is_returned_unchanged()
    {
        var transformer = new ControlPlaneEntitlementClaimsTransformer(new StubPolicy([]));
        ClaimsPrincipal principal = Authenticated("bob");

        ClaimsPrincipal result = await transformer.TransformAsync(principal);

        // Common path: no clone, no scope claim added.
        ReferenceEquals(result, principal).ShouldBeTrue();
        ScopeClaims(result).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_transform_is_idempotent_across_repeated_invocations()
    {
        var transformer = new ControlPlaneEntitlementClaimsTransformer(new StubPolicy([ControlPlaneScopes.RunsWrite]));
        ClaimsPrincipal principal = Authenticated("alice");

        await transformer.TransformAsync(principal);
        await transformer.TransformAsync(principal); // ASP.NET may invoke a transformer more than once per request.

        // The granted scope is stamped exactly once — not stacked.
        principal.Claims.Count(c => c.Type == "scope").ShouldBe(1);
        ScopeClaims(principal).ShouldBe([ControlPlaneScopes.RunsWrite]);
    }

    [TestMethod]
    public async Task An_unauthenticated_principal_is_left_unchanged()
    {
        var transformer = new ControlPlaneEntitlementClaimsTransformer(new StubPolicy([ControlPlaneScopes.RunsWrite]));
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // no authentication type → not authenticated

        ClaimsPrincipal result = await transformer.TransformAsync(principal);

        ReferenceEquals(result, principal).ShouldBeTrue();
        ScopeClaims(result).ShouldBeEmpty();
    }

    // A minimal policy that returns a fixed granted-scope set, isolating the transformer from the store/resolver.
    private sealed class StubPolicy(IReadOnlyList<string> granted) : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<string> ResolveGrantedScopes(ClaimsPrincipal? principal) => granted;
    }
}