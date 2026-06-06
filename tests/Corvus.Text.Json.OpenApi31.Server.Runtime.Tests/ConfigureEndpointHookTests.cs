// <copyright file="ConfigureEndpointHookTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Encodings.Web;
using CanonTests31.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Corvus.Text.Json.OpenApi31.Server.Runtime.Tests;

/// <summary>
/// Tests for the per-endpoint <c>ConfigureEndpoint</c> callback overload of the generated
/// <c>MapApiEndpoints</c> extension method.
/// </summary>
[TestClass]
public class ConfigureEndpointHookTests
{
    private sealed record RecordedEndpoint(
        string? OperationId,
        string MethodName,
        string HttpMethod,
        string RouteTemplate,
        bool IsCallback,
        int SecurityCount,
        IReadOnlyList<string> SchemeNames,
        IReadOnlyList<string?> SchemeTypes,
        IReadOnlyList<string> PolicyNames);

    [TestMethod]
    public async Task ConfigureEndpoint_InvokedOncePerEndpoint_WithAccurateDescriptors()
    {
        List<RecordedEndpoint> recorded = [];

        void Capture(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
        {
            recorded.Add(new RecordedEndpoint(
                endpoint.OperationId,
                endpoint.MethodName,
                endpoint.HttpMethod,
                endpoint.RouteTemplate,
                endpoint.IsCallback,
                endpoint.SecurityRequirements.Count,
                [.. endpoint.SecurityRequirements.SelectMany(s => s.Requirements).Select(r => r.SchemeName)],
                [.. endpoint.SecurityRequirements.SelectMany(s => s.Requirements).Select(r => r.SchemeType)],
                [.. endpoint.SecurityRequirements.SelectMany(s => s.Requirements).Select(r => r.PolicyName)]));
        }

        using IHost host = await BuildHostAsync(Capture, withAuth: false);

        // The callback fires once per generated endpoint.
        Assert.AreEqual(23, recorded.Count, "Callback should be invoked once per generated endpoint");

        // Descriptor fields are accurate for a known operation (GET /items -> ListItems).
        RecordedEndpoint listItems = recorded.Single(r => r.MethodName == "ListItems");
        Assert.AreEqual("GET", listItems.HttpMethod);
        Assert.AreEqual("/items", listItems.RouteTemplate);
        Assert.IsFalse(listItems.IsCallback, "Operations from the main paths are not callbacks");

        // covspec declares `[{bearerAuth}, {apiKeyAuth}]` on listItems: two alternatives (bearerAuth OR apiKeyAuth).
        Assert.AreEqual(2, listItems.SecurityCount, "ListItems declares two security alternatives (OR)");
        CollectionAssert.Contains(listItems.SchemeNames.ToList(), "bearerAuth");
        CollectionAssert.Contains(listItems.SchemeNames.ToList(), "apiKeyAuth");

        // The scheme type is resolved from components.securitySchemes.
        CollectionAssert.Contains(listItems.SchemeTypes.ToList(), "http");
        CollectionAssert.Contains(listItems.SchemeTypes.ToList(), "apiKey");

        // With no scopes the canonical policy name is just the scheme name.
        CollectionAssert.Contains(listItems.PolicyNames.ToList(), "bearerAuth");
        CollectionAssert.Contains(listItems.PolicyNames.ToList(), "apiKeyAuth");

        // Regular (paths) server: nothing is flagged as a callback, and only the declared op is secured.
        Assert.IsTrue(recorded.All(r => !r.IsCallback), "Regular server endpoints must not be flagged as callbacks");
        Assert.AreEqual(1, recorded.Count(r => r.SecurityCount > 0), "Only ListItems is secured in covspec");

        await host.StopAsync();
    }

    [TestMethod]
    public void EndpointSecurityRequirement_PolicyName_FormatsSchemeAndScopes()
    {
        // No scopes -> the policy name is the scheme name alone.
        EndpointSecurityRequirement noScopes = new("bearerAuth", System.Array.Empty<string>(), "http");
        Assert.AreEqual("bearerAuth", noScopes.PolicyName);
        Assert.AreEqual("http", noScopes.SchemeType);

        // Scopes -> "{scheme}:{scope+scope}".
        EndpointSecurityRequirement scoped = new("oauth2Auth", ["read", "write"], "oauth2");
        Assert.AreEqual("oauth2Auth:read+write", scoped.PolicyName);
        Assert.AreEqual("oauth2", scoped.SchemeType);
    }

    [TestMethod]
    public void EndpointSecurityRequirementSet_PolicyName_CombinesRequirementsWithAnd()
    {
        // A single-scheme alternative reuses the requirement's policy name.
        EndpointSecurityRequirementSet single = new([new("bearerAuth", System.Array.Empty<string>(), "http")], false);
        Assert.AreEqual("bearerAuth", single.PolicyName);

        // A multi-scheme alternative ANDs the requirement policy names.
        EndpointSecurityRequirementSet and = new(
            [new("bearerAuth", System.Array.Empty<string>(), "http"), new("oauth2Auth", ["read"], "oauth2")],
            false);
        Assert.AreEqual("bearerAuth && oauth2Auth:read", and.PolicyName);

        // The empty ({}) alternative is anonymous and has no policy name.
        EndpointSecurityRequirementSet optional = new(System.Array.Empty<EndpointSecurityRequirement>(), true);
        Assert.IsTrue(optional.IsOptional);
        Assert.AreEqual(string.Empty, optional.PolicyName);
    }

    [TestMethod]
    public async Task RequireDeclaredAuthorization_EnforcesDeclaredSecurity()
    {
        // The generated helper applies the declared security using the canonical policy names.
        static void Apply(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
            => builder.RequireDeclaredAuthorization(endpoint);

        using IHost host = await BuildHostAsync(Apply, withAuth: true, registerDeclaredPolicies: true);
        using HttpClient client = host.GetTestClient();

        // ListItems declares bearerAuth OR apiKeyAuth -> the helper requires the combined OR policy -> challenged.
        HttpResponseMessage secured = await client.GetAsync("/items");
        Assert.AreEqual(
            HttpStatusCode.Unauthorized,
            secured.StatusCode,
            "RequireDeclaredAuthorization should challenge the secured endpoint");

        // An endpoint with no declared security is marked AllowAnonymous by the helper -> reachable.
        HttpResponseMessage unsecured = await client.GetAsync("/items/item-123");
        Assert.AreEqual(
            HttpStatusCode.OK,
            unsecured.StatusCode,
            "RequireDeclaredAuthorization must leave undeclared endpoints anonymous");

        await host.StopAsync();
    }

    [TestMethod]
    public async Task ConfigureEndpoint_RequireAuthorization_EnforcesViaHook()
    {
        // Apply RequireAuthorization to a specific operation via the hook.
        static void ApplyAuthorization(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
        {
            if (endpoint.MethodName == "ListItems")
            {
                builder.RequireAuthorization();
            }
        }

        using IHost host = await BuildHostAsync(ApplyAuthorization, withAuth: true);
        using HttpClient client = host.GetTestClient();

        // The endpoint secured through the hook is challenged (no authenticated user) -> 401.
        HttpResponseMessage secured = await client.GetAsync("/items");
        Assert.AreEqual(
            HttpStatusCode.Unauthorized,
            secured.StatusCode,
            "Authorization applied through the hook should challenge the endpoint");

        // An endpoint left untouched by the hook remains anonymous and reachable.
        HttpResponseMessage unsecured = await client.GetAsync("/items/item-123");
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            unsecured.StatusCode,
            "Endpoints not configured by the hook must stay anonymous");
        Assert.AreEqual(HttpStatusCode.OK, unsecured.StatusCode);

        await host.StopAsync();
    }

    private static async Task<IHost> BuildHostAsync(ConfigureEndpoint configureEndpoint, bool withAuth, bool registerDeclaredPolicies = false)
    {
        HostBuilder builder = new();
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddRouting();
                if (withAuth)
                {
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, UnauthenticatedHandler>("Test", _ => { });
                    if (registerDeclaredPolicies)
                    {
                        // listItems is `bearerAuth OR apiKeyAuth`, so the helper requires the combined OR policy.
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("bearerAuth || apiKeyAuth", p => p.RequireAuthenticatedUser());
                        });
                    }
                    else
                    {
                        services.AddAuthorization();
                    }
                }
            });
            webHost.Configure(app =>
            {
                app.UseRouting();
                if (withAuth)
                {
                    app.UseAuthentication();
                    app.UseAuthorization();
                }

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapApiEndpoints(
                        new MockDefaultHandler(),
                        new MockItemsHandler(),
                        new MockSearchHandler(),
                        configureEndpoint);
                });
            });
        });

        return await builder.StartAsync();
    }

    /// <summary>
    /// An authentication handler that never authenticates, so that endpoints requiring
    /// authorization are challenged with 401.
    /// </summary>
    private sealed class UnauthenticatedHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public UnauthenticatedHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}