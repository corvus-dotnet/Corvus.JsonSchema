// <copyright file="CliAvailabilityRequestsIntegrationTests.cs" company="Endjin Limited">
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
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>availability-requests</c> command surface (design §7.8), driven in-process against an
/// authenticated control-plane server over loopback HTTP. A requester who does not administer the target environment asks
/// for a version to be made available; the environment's administrator approves it from their inbox (making it available)
/// or denies it. The server maps the CLI's <c>--token</c> to a deployment identity (sys:tenant=&lt;token&gt;) and a requester
/// subject (sub=&lt;token&gt;), so the tenant that creates an environment administers it.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task An_availability_request_is_submitted_approved_and_makes_the_version_available_over_http()
    {
        await using Host host = await StartAvailabilityRequestsAsync();

        // acme provisions the environment (granting itself administration); 'flow' v1 is seeded source-less (ready).
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        // globex (not an administrator of 'production') requests that flow v1 be made available there.
        (int submitExit, string submitOut, _) = await RunAsync(host, "availability-requests", "submit", "flow", "1", "production", "--token", "globex");
        submitExit.ShouldBe(0);
        submitOut.ShouldContain("Pending");
        string id;
        using (Stj.JsonDocument submitted = Stj.JsonDocument.Parse(submitOut))
        {
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // acme sees it in the approver inbox.
        (int inboxExit, string inboxOut, _) = await RunAsync(host, "availability-requests", "list", "--inbox", "--token", "acme", "--output", "json");
        inboxExit.ShouldBe(0);
        inboxOut.ShouldContain(id);

        // acme approves → the request is Approved and the version is now available in 'production'.
        (int approveExit, string approveOut, _) = await RunAsync(host, "availability-requests", "approve", id, "--token", "acme");
        approveExit.ShouldBe(0);
        approveOut.ShouldContain("Approved");

        (int verExit, string verOut, _) = await RunAsync(host, "availability", "versions", "production", "--token", "acme", "--output", "json");
        verExit.ShouldBe(0);
        verOut.ShouldContain("flow");
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_approve_an_availability_request_over_http()
    {
        await using Host host = await StartAvailabilityRequestsAsync();
        (await RunAsync(host, "environments", "create", "production", "--token", "acme")).Exit.ShouldBe(0);

        (int submitExit, string submitOut, _) = await RunAsync(host, "availability-requests", "submit", "flow", "1", "production", "--token", "globex");
        submitExit.ShouldBe(0);
        string id;
        using (Stj.JsonDocument submitted = Stj.JsonDocument.Parse(submitOut))
        {
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // globex does not administer 'production': the approve is refused (403 → non-zero exit).
        (int exit, _, string stderr) = await RunAsync(host, "availability-requests", "approve", id, "--token", "globex");
        exit.ShouldNotBe(0);
        stderr.ShouldNotBeNullOrEmpty();
    }

    private static async Task<Host> StartAvailabilityRequestsAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new SecuredWorkflowManagement(store, "ops", CompleteResumer, clock);

        // credentials: null skips the §13 catalog-time gate so a source-less version publishes cleanly; the version is
        // source-less, so it is ready in any environment — the request/approval flow is what we exercise.
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        SecurityTagSet founder = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme")]);
        await catalog.AddAsync(Package("flow"), new CatalogOwner("Team", "team@example.com", null, null), default, founder, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(AvailabilityRequestsBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AvailabilityRequestsBearerHandler>(AvailabilityRequestsBearerHandler.SchemeName, _ => { });
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

    private sealed class AvailabilityRequestsBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AvailabilityRequestsBearer";

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
            identity.AddClaim(new Claim("sub", token));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}