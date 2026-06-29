// <copyright file="CliAvailabilityIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>availability</c> ("promotion") command surface, driven in-process against an
/// authenticated control-plane server over loopback HTTP. Making a version available is governed by the target
/// environment's administrators: the server maps the CLI's <c>--token</c> to a deployment identity (sys:tenant=&lt;token&gt;),
/// so the tenant that creates an environment administers it.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Availability_is_made_listed_and_withdrawn_over_http()
    {
        await using Host host = await StartAvailabilityAsync();

        // acme provisions the environment (granting itself administration); the version 'flow' v1 is seeded source-less.
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        (int makeExit, string makeOut, _) = await RunAsync(host, "availability", "make", "flow", "1", "production", "--token", "acme");
        makeExit.ShouldBe(0);
        makeOut.ShouldContain("production");

        (int envExit, string envOut, _) = await RunAsync(host, "availability", "environments", "flow", "1", "--token", "acme", "--output", "json");
        envExit.ShouldBe(0);
        envOut.ShouldContain("production");

        (int verExit, string verOut, _) = await RunAsync(host, "availability", "versions", "production", "--token", "acme", "--output", "json");
        verExit.ShouldBe(0);
        verOut.ShouldContain("flow");

        (int withdrawExit, string withdrawOut, _) = await RunAsync(host, "availability", "withdraw", "flow", "1", "production", "--token", "acme");
        withdrawExit.ShouldBe(0);
        withdrawOut.ShouldContain("Withdrew version 1 of 'flow' from 'production'.");
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_make_a_version_available_over_http()
    {
        await using Host host = await StartAvailabilityAsync();
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        // globex does not administer 'production': the make is refused (403 → non-zero exit).
        (int exit, _, string stderr) = await RunAsync(host, "availability", "make", "flow", "1", "production", "--token", "globex");
        exit.ShouldNotBe(0);
        stderr.ShouldNotBeNullOrEmpty();
    }

    private static async Task<Host> StartAvailabilityAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new SecuredWorkflowManagement(store, "ops", CompleteResumer, clock);

        // credentials: null skips the §13 catalog-time gate so a source-less version is published cleanly; the version
        // is source-less, so it is ready in any environment (no credentials needed) — promotion is what we exercise.
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        // Seed version 1 of 'flow' (source-less, owned by acme) so there is something to make available.
        SecurityTagSet founder = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme")]);
        await catalog.AddAsync(Package("flow"), new CatalogOwner("Team", "team@example.com", null, null), default, founder, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(AvailabilityBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AvailabilityBearerHandler>(AvailabilityBearerHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Host(app, store, clock, app.Urls.First());

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private sealed class AvailabilityBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AvailabilityBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = this.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = header["Bearer ".Length..].Trim();
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:write availability:read availability:write"));
            identity.AddClaim(new Claim("tenant", token));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}