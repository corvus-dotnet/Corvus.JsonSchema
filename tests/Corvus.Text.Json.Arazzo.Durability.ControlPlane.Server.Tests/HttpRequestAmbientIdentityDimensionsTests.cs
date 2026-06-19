// <copyright file="HttpRequestAmbientIdentityDimensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Covers <see cref="HttpRequestAmbientIdentityDimensions"/> (design §16.5.5): mapping a request's vanity host or a
/// gateway header to its ambient <c>sys:</c> dimensions against an authoritative allow-list, failing closed on anything
/// unconfigured — the trust boundary that makes a context-derived dimension safe.
/// </summary>
[TestClass]
public sealed class HttpRequestAmbientIdentityDimensionsTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> HostAllowList = new Dictionary<string, IReadOnlyList<SecurityTag>>
    {
        ["acme.host.example"] = [new SecurityTag("sys:tenant", "acme")],
        ["globex.host.example"] = [new SecurityTag("sys:tenant", "globex")],
    };

    [TestMethod]
    public void A_known_host_resolves_its_tenant()
    {
        var provider = HttpRequestAmbientIdentityDimensions.ByHost(Accessor(c => c.Request.Host = new HostString("acme.host.example")), HostAllowList);
        provider.Resolve().Tags.ShouldBe([new SecurityTag("sys:tenant", "acme")]);
    }

    [TestMethod]
    public void Host_matching_is_case_insensitive()
    {
        var provider = HttpRequestAmbientIdentityDimensions.ByHost(Accessor(c => c.Request.Host = new HostString("ACME.Host.Example")), HostAllowList);
        provider.Resolve().Tags.ShouldBe([new SecurityTag("sys:tenant", "acme")]);
    }

    [TestMethod]
    public void An_unknown_host_fails_closed()
    {
        var provider = HttpRequestAmbientIdentityDimensions.ByHost(Accessor(c => c.Request.Host = new HostString("evil.attacker.example")), HostAllowList);
        provider.Resolve().ShouldBeSameAs(AmbientDimensionSet.Empty);
    }

    [TestMethod]
    public void No_request_context_fails_closed()
    {
        var provider = HttpRequestAmbientIdentityDimensions.ByHost(new HttpContextAccessor(), HostAllowList);
        provider.Resolve().ShouldBeSameAs(AmbientDimensionSet.Empty);
    }

    [TestMethod]
    public void The_governed_keys_are_derived_from_the_allow_list()
    {
        var provider = HttpRequestAmbientIdentityDimensions.ByHost(new HttpContextAccessor(), HostAllowList);
        provider.GovernedKeys.ShouldBe(["sys:tenant"]);
    }

    [TestMethod]
    public void A_gateway_header_resolves_only_an_allow_listed_value()
    {
        IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> headerAllowList = new Dictionary<string, IReadOnlyList<SecurityTag>>
        {
            ["acme"] = [new SecurityTag("sys:tenant", "acme")],
        };

        var resolved = HttpRequestAmbientIdentityDimensions.ByHeader(Accessor(c => c.Request.Headers["X-Tenant"] = "acme"), "X-Tenant", headerAllowList);
        resolved.Resolve().Tags.ShouldBe([new SecurityTag("sys:tenant", "acme")]);

        // A spoofed / unknown header value is not in the allow-list → fail closed (no tenant, no cross-tenant access).
        var spoofed = HttpRequestAmbientIdentityDimensions.ByHeader(Accessor(c => c.Request.Headers["X-Tenant"] = "globex"), "X-Tenant", headerAllowList);
        spoofed.Resolve().ShouldBeSameAs(AmbientDimensionSet.Empty);

        // A missing header → fail closed.
        var absent = HttpRequestAmbientIdentityDimensions.ByHeader(Accessor(_ => { }), "X-Tenant", headerAllowList);
        absent.Resolve().ShouldBeSameAs(AmbientDimensionSet.Empty);
    }

    private static IHttpContextAccessor Accessor(Action<DefaultHttpContext> configure)
    {
        var context = new DefaultHttpContext();
        configure(context);
        return new HttpContextAccessor { HttpContext = context };
    }
}