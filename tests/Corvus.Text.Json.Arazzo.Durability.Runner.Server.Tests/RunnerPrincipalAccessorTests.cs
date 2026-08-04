// <copyright file="RunnerPrincipalAccessorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Coverage of how a request's machine principal is established (ADR 0065 decision 2). The value resolved here is the
/// lease owner and the subject of every binding lookup, so it has to be the same value the control plane bound the
/// runner's authorization to at registration. A principal resolved differently by the two surfaces is not a mismatch
/// that surfaces as an error: the runner resolves to no bindings and is simply offered nothing, which is exactly what a
/// correctly-refused runner looks like.
/// </summary>
[TestClass]
public sealed class RunnerPrincipalAccessorTests
{
    /// <summary>
    /// The claims a Keycloak client-credentials token actually carries, taken from the demo realm's own
    /// <c>arazzo-runner</c> client: <c>sub</c> is the service account's user id and <c>azp</c> is the client id.
    /// </summary>
    private const string ServiceAccountUserId = "91d9fc72-50b2-4d16-80d6-1945021b40a3";
    private const string ClientId = "arazzo-runner";

    [TestMethod]
    public void A_client_credentials_principal_resolves_to_the_client_it_registered_as()
    {
        // What the control plane binds the authorization to is the client id, so that is what the API must resolve.
        // Reading the subject instead yields the service account's user id, which matches no authorization record.
        var accessor = new RunnerPrincipalAccessor(ContextFor(
            new Claim("sub", ServiceAccountUserId),
            new Claim("azp", ClientId)));

        accessor.Resolve().ShouldBe(ClientId);
    }

    [TestMethod]
    public void A_principal_naming_its_client_only_as_client_id_resolves_to_it()
    {
        // Not every issuer emits azp; the identity is the same client either way.
        var accessor = new RunnerPrincipalAccessor(ContextFor(
            new Claim("sub", ServiceAccountUserId),
            new Claim("client_id", ClientId)));

        accessor.Resolve().ShouldBe(ClientId);
    }

    [TestMethod]
    public void A_principal_carrying_no_client_claim_resolves_to_its_subject()
    {
        // A deployment identifying runners by certificate has a subject and no client claims at all.
        var accessor = new RunnerPrincipalAccessor(ContextFor(new Claim("sub", ServiceAccountUserId)));

        accessor.Resolve().ShouldBe(ServiceAccountUserId);
    }

    [TestMethod]
    public void A_deployment_naming_its_claim_gets_that_claim_and_no_other()
    {
        // Naming a claim is an instruction, not a preference: an issuer whose azp means something else is why the
        // option exists, so the standard order must not reassert itself over the top of it.
        var accessor = new RunnerPrincipalAccessor(
            ContextFor(new Claim("sub", ServiceAccountUserId), new Claim("azp", ClientId)),
            new RunnerApiOptions { PrincipalClaimType = "sub" });

        accessor.Resolve().ShouldBe(ServiceAccountUserId);
    }

    [TestMethod]
    public void An_unauthenticated_request_resolves_to_no_principal()
    {
        var accessor = new RunnerPrincipalAccessor(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        accessor.Resolve().ShouldBeNull();
    }

    private static IHttpContextAccessor ContextFor(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };

        return new HttpContextAccessor { HttpContext = context };
    }
}