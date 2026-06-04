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
        int SecurityCount);

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
                endpoint.SecurityRequirements.Count));
        }

        using IHost host = await BuildHostAsync(Capture, withAuth: false);

        // The callback fires once per generated endpoint.
        Assert.AreEqual(23, recorded.Count, "Callback should be invoked once per generated endpoint");

        // Descriptor fields are accurate for a known operation (GET /items -> ListItems).
        RecordedEndpoint listItems = recorded.Single(r => r.MethodName == "ListItems");
        Assert.AreEqual("GET", listItems.HttpMethod);
        Assert.AreEqual("/items", listItems.RouteTemplate);
        Assert.IsFalse(listItems.IsCallback, "Operations from the main paths are not callbacks");

        // Regular (paths) server: nothing is flagged as a callback. OpenAPI 3.1 does not extract
        // security, so every descriptor surfaces an empty requirements list.
        Assert.IsTrue(recorded.All(r => !r.IsCallback), "Regular server endpoints must not be flagged as callbacks");
        Assert.IsTrue(recorded.All(r => r.SecurityCount == 0), "OpenAPI 3.1 surfaces no security requirements");

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