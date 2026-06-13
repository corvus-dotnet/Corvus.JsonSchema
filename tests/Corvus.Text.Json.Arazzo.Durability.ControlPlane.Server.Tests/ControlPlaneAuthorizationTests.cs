// <copyright file="ControlPlaneAuthorizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests control-plane operation authorization: each endpoint demands its declared capability scope as a
/// policy, the deployment supplies the authentication scheme + how a principal acquires scopes, and an
/// unauthenticated or under-scoped caller is rejected (401 / 403) before the handler runs.
/// </summary>
[TestClass]
public sealed class ControlPlaneAuthorizationTests
{
    [TestMethod]
    public async Task An_unauthenticated_caller_is_challenged()
    {
        await using Secured host = await StartSecuredAsync();

        HttpResponseMessage response = await host.GetAsync("/runs", scopes: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task A_caller_with_the_wrong_scope_is_forbidden()
    {
        await using Secured host = await StartSecuredAsync();

        // runs:read is required to list runs; this caller only holds catalog:read.
        HttpResponseMessage response = await host.GetAsync("/runs", scopes: ControlPlaneScopes.CatalogRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_caller_with_the_required_scope_is_allowed()
    {
        await using Secured host = await StartSecuredAsync();

        HttpResponseMessage response = await host.GetAsync("/runs", scopes: ControlPlaneScopes.RunsRead);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Scope_grants_are_space_delimited_and_per_endpoint()
    {
        await using Secured host = await StartSecuredAsync();

        // One space-delimited grant carrying several scopes satisfies each endpoint's own requirement.
        const string grant = $"{ControlPlaneScopes.RunsRead} {ControlPlaneScopes.CatalogRead}";

        (await host.GetAsync("/runs", grant)).StatusCode.ShouldBe(HttpStatusCode.OK);          // runs:read
        (await host.GetAsync("/catalog", grant)).StatusCode.ShouldBe(HttpStatusCode.OK);       // catalog:read

        // …but a catalog:read-only caller cannot list runs.
        (await host.GetAsync("/runs", ControlPlaneScopes.CatalogRead)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Secured> StartSecuredAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // The deployment owns authentication: a header-driven scheme stands in for JWT bearer / OIDC / mTLS.
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), requireAuthorization: true);
        await app.StartAsync();

        return new Secured(app, app.GetTestClient());
    }

    private sealed class Secured(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public async Task<HttpResponseMessage> GetAsync(string path, string? scopes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (scopes is not null)
            {
                request.Headers.Add(TestAuthHandler.ScopesHeader, scopes);
            }

            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// A test authentication scheme: an <c>X-Test-Scopes</c> header authenticates the caller and carries its
    /// granted scopes (a stand-in for a real bearer/OIDC token); no header means an unauthenticated request.
    /// </summary>
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string ScopesHeader = "X-Test-Scopes";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopesHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}