// <copyright file="ConfigureEndpointHookTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Encodings.Web;
using CanonTests32.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Tests for the per-endpoint <c>ConfigureEndpoint</c> callback overload of the generated
/// <c>MapApiEndpoints</c> extension method.
/// </summary>
[TestClass]
public class ConfigureEndpointHookTests
{
    /// <summary>
    /// Captures the descriptor fields surfaced to the callback so they can be asserted.
    /// </summary>
    private sealed record RecordedEndpoint(
        string? OperationId,
        string MethodName,
        string HttpMethod,
        string RouteTemplate,
        bool IsCallback,
        int SecurityCount,
        IReadOnlyList<string> SchemeNames);

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
                [.. endpoint.SecurityRequirements.Select(r => r.SchemeName)]));
        }

        using IHost host = await BuildHostAsync(Capture, withAuth: false);

        // The callback fires once per generated endpoint.
        Assert.AreEqual(44, recorded.Count, "Callback should be invoked once per generated endpoint");

        // Descriptor fields are accurate for a known operation (GET /items -> ListItems).
        RecordedEndpoint listItems = recorded.Single(r => r.MethodName == "ListItems");
        Assert.AreEqual("listItems", listItems.OperationId);
        Assert.AreEqual("GET", listItems.HttpMethod);
        Assert.AreEqual("/items", listItems.RouteTemplate);
        Assert.IsFalse(listItems.IsCallback, "Operations from the main paths are not callbacks");

        // Security requirements are surfaced (covspec declares bearerAuth + apiKeyAuth on listItems).
        Assert.AreEqual(2, listItems.SecurityCount, "ListItems declares two security requirements");
        CollectionAssert.Contains(listItems.SchemeNames.ToList(), "bearerAuth");
        CollectionAssert.Contains(listItems.SchemeNames.ToList(), "apiKeyAuth");

        // Regular (paths) server: nothing is flagged as a callback, and only the declared op is secured.
        Assert.IsTrue(recorded.All(r => !r.IsCallback), "Regular server endpoints must not be flagged as callbacks");
        Assert.AreEqual(1, recorded.Count(r => r.SecurityCount > 0), "Only ListItems is secured in covspec");

        await host.StopAsync();
    }

    [TestMethod]
    public async Task ConfigureEndpoint_RequireAuthorization_EnforcesDeclaredSecurity()
    {
        // Apply RequireAuthorization to any endpoint that declares security, via the hook.
        static void ApplyAuthorization(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
        {
            if (endpoint.SecurityRequirements.Count > 0)
            {
                builder.RequireAuthorization();
            }
        }

        using IHost host = await BuildHostAsync(ApplyAuthorization, withAuth: true);
        using HttpClient client = host.GetTestClient();

        // The secured endpoint is now challenged (no authenticated user) -> 401.
        HttpResponseMessage secured = await client.GetAsync("/items");
        Assert.AreEqual(
            HttpStatusCode.Unauthorized,
            secured.StatusCode,
            "Authorization applied through the hook should challenge the secured endpoint");

        // An endpoint with no declared security remains anonymous and reachable.
        HttpResponseMessage unsecured = await client.GetAsync("/items/item-123");
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            unsecured.StatusCode,
            "Endpoints without declared security must stay anonymous");
        Assert.AreEqual(HttpStatusCode.OK, unsecured.StatusCode);

        await host.StopAsync();
    }

    private static async Task<IHost> BuildHostAsync(ConfigureEndpoint configureEndpoint, bool withAuth)
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
                    services.AddAuthorization();
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