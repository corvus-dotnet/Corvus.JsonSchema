// <copyright file="CliEnvironmentsIntegrationTests.cs" company="Endjin Limited">
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
/// End-to-end tests for the <c>environments</c> command surface, driven in-process against an authenticated control-plane
/// server over loopback HTTP. Environments are governed by current-administrator membership (creating one grants the
/// creator administration), so the server maps the caller's bearer token (the CLI's <c>--token</c>) to a deployment
/// identity and grants the <c>environments:*</c> scopes.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Environment_lists_creates_gets_updates_and_deletes_over_http()
    {
        await using Host host = await StartEnvironmentsAsync();

        // acme creates an environment and is granted its administration.
        (int createExit, string createOut, _) = await RunAsync(host, "environments", "create", "production", "--display-name", "Production", "--token", "acme");
        createExit.ShouldBe(0);
        createOut.ShouldContain("production");

        (int getExit, string getOut, _) = await RunAsync(host, "environments", "get", "production", "--token", "acme");
        getExit.ShouldBe(0);
        getOut.ShouldContain("Production");

        (int listExit, string listOut, _) = await RunAsync(host, "environments", "list", "--token", "acme", "--output", "json");
        listExit.ShouldBe(0);
        listOut.ShouldContain("production");

        // The creator (an administrator) updates the display name (merge; current-administrator only).
        (int updateExit, string updateOut, _) = await RunAsync(host, "environments", "update", "production", "--description", "Live", "--token", "acme");
        updateExit.ShouldBe(0);
        updateOut.ShouldContain("Live");

        (int deleteExit, string deleteOut, _) = await RunAsync(host, "environments", "delete", "production", "--token", "acme");
        deleteExit.ShouldBe(0);
        deleteOut.ShouldContain("Deleted environment 'production'.");

        (int goneExit, _, _) = await RunAsync(host, "environments", "get", "production", "--token", "acme");
        goneExit.ShouldNotBe(0);
    }

    [TestMethod]
    public async Task Environment_administration_lists_adds_removes_and_transfers_over_http()
    {
        await using Host host = await StartEnvironmentsAsync();
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        // acme (the creator) is the sole administrator.
        (int listExit, string listOut, _) = await RunAsync(host, "environments", "administrators", "list", "production", "--token", "acme");
        listExit.ShouldBe(0);
        listOut.ShouldContain("acme");

        // acme adds globex as a co-administrator.
        (int addExit, string addOut, _) = await RunAsync(host, "environments", "administrators", "add", "production", "tenant", "globex", "--token", "acme");
        addExit.ShouldBe(0);
        addOut.ShouldContain("globex");
        addOut.ShouldContain("acme");

        // globex, now an administrator, removes acme by its identity digest (the set never empties — globex remains).
        string acmeDigest = SecurityIdentityDigest.Compute(SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme")]))!;
        (int removeExit, string removeOut, _) = await RunAsync(host, "environments", "administrators", "remove", "production", acmeDigest, "--token", "globex");
        removeExit.ShouldBe(0);
        removeOut.ShouldContain("globex");
        removeOut.ShouldNotContain("acme");

        // globex transfers administration back to acme (replacing the whole set).
        (int transferExit, string transferOut, _) = await RunAsync(host, "environments", "administrators", "transfer", "production", "--admin", "tenant=acme", "--token", "globex");
        transferExit.ShouldBe(0);
        transferOut.ShouldContain("acme");
        transferOut.ShouldNotContain("globex");
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_govern_an_environment_over_http()
    {
        await using Host host = await StartEnvironmentsAsync();
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        // globex is not an administrator of 'production': the update is refused (403 → non-zero exit).
        (int exit, _, string stderr) = await RunAsync(host, "environments", "update", "production", "--description", "nope", "--token", "globex");
        exit.ShouldNotBe(0);
        stderr.ShouldNotBeNullOrEmpty();
    }

    private static async Task<Host> StartEnvironmentsAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new SecuredWorkflowManagement(store, "ops", CompleteResumer, clock);
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(EnvironmentsBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, EnvironmentsBearerHandler>(EnvironmentsBearerHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();

        // The environment stores default to in-memory; the row-security policy maps the bearer token to a deployment
        // identity (sys:tenant=<token>) so create-grants-admin and the current-administrator gate are driven by --token.
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Host(app, store, clock, app.Urls.First());

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private sealed class EnvironmentsBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "EnvironmentsBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = this.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = header["Bearer ".Length..].Trim();
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write"));
            identity.AddClaim(new Claim("tenant", token));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}