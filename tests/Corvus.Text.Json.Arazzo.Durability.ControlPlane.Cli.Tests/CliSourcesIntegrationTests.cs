// <copyright file="CliSourcesIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>sources</c> command surface, driven in-process against an authenticated control-plane
/// server over loopback HTTP. Sources are reach-scoped (not governed): the server maps the caller's bearer token (the
/// CLI's <c>--token</c>) to a deployment identity (sys:tenant=&lt;token&gt;) and grants the <c>sources:*</c> scopes, so a
/// source registered by one tenant is invisible to another.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Source_lists_registers_gets_updates_and_deletes_over_http()
    {
        await using Host host = await StartSourcesAsync();
        string docPath = await WriteTempDocumentAsync("""{"openapi":"3.1.0","info":{"title":"Petstore"}}""");
        try
        {
            (int registerExit, string registerOut, _) = await RunAsync(host, "sources", "register", "petstore", "--type", "openapi", "--document", docPath, "--display-name", "Pet Store", "--token", "acme");
            registerExit.ShouldBe(0);
            registerOut.ShouldContain("petstore");
            registerOut.ShouldContain("Petstore"); // the document round-trips on the single response

            (int getExit, string getOut, _) = await RunAsync(host, "sources", "get", "petstore", "--token", "acme");
            getExit.ShouldBe(0);
            getOut.ShouldContain("Pet Store");
            getOut.ShouldContain("Petstore");

            (int listExit, string listOut, _) = await RunAsync(host, "sources", "list", "--token", "acme", "--output", "json");
            listExit.ShouldBe(0);
            listOut.ShouldContain("petstore");

            // A metadata-only update keeps the stored document (no --document needed to rename).
            (int updateExit, string updateOut, _) = await RunAsync(host, "sources", "update", "petstore", "--description", "The pet store API.", "--token", "acme");
            updateExit.ShouldBe(0);
            updateOut.ShouldContain("The pet store API.");
            updateOut.ShouldContain("Petstore"); // document carried forward

            (int deleteExit, string deleteOut, _) = await RunAsync(host, "sources", "delete", "petstore", "--token", "acme");
            deleteExit.ShouldBe(0);
            deleteOut.ShouldContain("Deleted source 'petstore'.");

            (int goneExit, _, _) = await RunAsync(host, "sources", "get", "petstore", "--token", "acme");
            goneExit.ShouldNotBe(0);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [TestMethod]
    public async Task Source_management_tags_are_settable_on_register_and_re_taggable_on_update()
    {
        await using Host host = await StartSourcesAsync();
        string docPath = await WriteTempDocumentAsync("""{"openapi":"3.1.0","info":{"title":"Petstore"}}""");
        try
        {
            (int registerExit, string registerOut, _) = await RunAsync(host, "sources", "register", "petstore", "--type", "openapi", "--document", docPath, "--manage", "team=payments", "--token", "acme");
            registerExit.ShouldBe(0);
            registerOut.ShouldContain("payments");

            // --manage re-tags the non-internal labels on update; the old one is replaced (the document is carried forward).
            (int updateExit, string updateOut, _) = await RunAsync(host, "sources", "update", "petstore", "--manage", "team=billing", "--token", "acme");
            updateExit.ShouldBe(0);
            updateOut.ShouldContain("billing");
            updateOut.ShouldNotContain("payments");

            // Omitting --manage carries the tags forward (a description-only update keeps billing).
            (int keepExit, string keepOut, _) = await RunAsync(host, "sources", "update", "petstore", "--description", "The pet store API.", "--token", "acme");
            keepExit.ShouldBe(0);
            keepOut.ShouldContain("billing");
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [TestMethod]
    public async Task Updating_a_source_with_a_new_document_rotates_it_over_http()
    {
        await using Host host = await StartSourcesAsync();

        // Distinct, non-overlapping titles (Shouldly's ShouldNotContain is case-insensitive and the name is "petstore",
        // so the marker must not be a sub/superstring of either the other marker or the name).
        string docPath = await WriteTempDocumentAsync("""{"openapi":"3.1.0","info":{"title":"alpha-spec"}}""");
        string rotatedPath = await WriteTempDocumentAsync("""{"openapi":"3.1.0","info":{"title":"bravo-spec"}}""");
        try
        {
            (await RunAsync(host, "sources", "register", "petstore", "--type", "openapi", "--document", docPath, "--token", "acme")).Exit.ShouldBe(0);

            (int rotateExit, string rotateOut, _) = await RunAsync(host, "sources", "update", "petstore", "--document", rotatedPath, "--token", "acme");
            rotateExit.ShouldBe(0);
            rotateOut.ShouldContain("bravo-spec");

            (int getExit, string getOut, _) = await RunAsync(host, "sources", "get", "petstore", "--token", "acme");
            getExit.ShouldBe(0);
            getOut.ShouldContain("bravo-spec");
            getOut.ShouldNotContain("alpha-spec"); // the old document is gone
        }
        finally
        {
            File.Delete(docPath);
            File.Delete(rotatedPath);
        }
    }

    private static async Task<string> WriteTempDocumentAsync(string json)
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static async Task<Host> StartSourcesAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new SecuredWorkflowManagement(store, "ops", CompleteResumer, clock);
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(SourcesBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SourcesBearerHandler>(SourcesBearerHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();

        // The source store defaults to in-memory; the row-security policy maps the bearer token to a deployment identity
        // (sys:tenant=<token>) so reach scoping is driven by --token (a source registered by one tenant is invisible to another).
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Host(app, store, clock, app.Urls.First());

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private sealed class SourcesBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "SourcesBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = this.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = header["Bearer ".Length..].Trim();
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "sources:read sources:write"));
            identity.AddClaim(new Claim("tenant", token));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}