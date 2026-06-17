// <copyright file="CliAdministratorsIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text;
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
/// End-to-end tests for the <c>administrators</c> command surface, driven in-process against an authenticated
/// control-plane server over loopback HTTP. Administration is governed by current-administrator membership, so the
/// server is wired with an administrator store and a row-security policy that maps the caller's bearer token to a
/// deployment identity (the CLI's <c>--token</c> stands in for the principal).
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Administrator_set_lists_adds_removes_and_transfers_over_http()
    {
        await using Host host = await StartAdministeredAsync();

        // acme (the founder, established as version 1's administrator) lists itself.
        (int listExit, string listOut, _) = await RunAsync(host, "administrators", "list", "flow", "--token", "acme", "--output", "json");
        listExit.ShouldBe(0);
        listOut.ShouldContain("acme");

        // acme adds globex as a co-administrator.
        (int addExit, string addOut, _) = await RunAsync(host, "administrators", "add", "flow", "tenant", "globex", "--token", "acme");
        addExit.ShouldBe(0);
        addOut.ShouldContain("globex");
        addOut.ShouldContain("acme");

        // globex, now an administrator, removes acme — the set never empties because globex remains.
        (int removeExit, string removeOut, _) = await RunAsync(host, "administrators", "remove", "flow", "tenant", "acme", "--token", "globex");
        removeExit.ShouldBe(0);
        removeOut.ShouldContain("globex");
        removeOut.ShouldNotContain("acme");

        // globex transfers administration back to acme (replacing the whole set).
        (int transferExit, string transferOut, _) = await RunAsync(host, "administrators", "transfer", "flow", "--admin", "tenant=acme", "--token", "globex");
        transferExit.ShouldBe(0);
        transferOut.ShouldContain("acme");
        transferOut.ShouldNotContain("globex");
    }

    [TestMethod]
    public async Task A_non_administrator_is_refused_over_http()
    {
        await using Host host = await StartAdministeredAsync();

        // globex is not an administrator of 'flow': the add is refused (403 → non-zero exit).
        (int exit, _, string stderr) = await RunAsync(host, "administrators", "add", "flow", "tenant", "globex", "--token", "globex");
        exit.ShouldBe(1);
        stderr.ShouldNotBeNullOrEmpty();
    }

    private static async Task<Host> StartAdministeredAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new WorkflowManagementClient(store, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(
            new InMemoryWorkflowCatalogStore(clock),
            store,
            "ops",
            credentials: null,
            administrators: new InMemoryWorkflowAdministratorStore());

        // acme establishes the base id by publishing version 1, becoming its sole administrator.
        SecurityTagSet founder = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme")]);
        await catalog.AddAsync(Package("flow"), new CatalogOwner("Team", "team@example.com", null, null), default, founder, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(BearerIdentityHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerIdentityHandler>(BearerIdentityHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        // The subject a granted access request keys on is the caller's tenant identity (the CLI's --token), so the
        // access-requests command tests can submit/approve/self-serve over the same authenticated host.
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), requireAuthorization: true, rowSecurity: new TenantIdentityPolicy(), accessRequestSubjectClaimType: "tenant");
        await app.StartAsync();

        return new Host(app, store, clock, app.Urls.First());

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        return CatalogPackage.Build(workflow, []);
    }

    /// <summary>Maps the caller's bearer token to a deployment identity (<c>sys:tenant=&lt;token&gt;</c>) and grants the
    /// administration scopes, so the CLI's <c>--token</c> drives current-administrator membership; the grant mapping is
    /// the base class default (grant {tenant, value} ↔ sys:tenant=value).</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class BearerIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "BearerIdentity";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = this.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = header["Bearer ".Length..].Trim();
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "administrators:read administrators:write"));
            identity.AddClaim(new Claim("tenant", token));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}