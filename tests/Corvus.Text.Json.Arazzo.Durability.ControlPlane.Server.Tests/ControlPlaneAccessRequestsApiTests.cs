// <copyright file="ControlPlaneAccessRequestsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the control-plane access-request API (§16.5) over <c>/accessRequests</c>: submit, the approver queue, the
/// §15-administrator approval gate, and the time-bound grant. The test identity (the <c>tenant</c> claim) doubles as
/// the requesting subject, so a request submitted by 'alice' is approved by the workflow's administrator 'boss'.
/// </summary>
[TestClass]
public sealed class ControlPlaneAccessRequestsApiTests
{
    private const string Auth = "any"; // any scope value authenticates; these operations require no specific scope.

    [TestMethod]
    public async Task A_request_is_submitted_queued_and_approved_by_the_administrator()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "boss");

        // alice submits a request for run access to 'flow' (pending — she is not eligible to self-elevate).
        string id;
        using (Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, "alice")))
        {
            submitted.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
            submitted.RootElement.GetProperty("subjectClaimValue").GetString().ShouldBe("alice");
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // boss (the workflow's administrator) sees alice's request in the queue.
        using (Stj.JsonDocument queue = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests?baseWorkflowId=flow", Auth, "boss")))
        {
            queue.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain(id);
        }

        // boss approves; the grant is time-boxed (grantedUntil set). The decision note is optional, so this is a
        // bodyless POST — the generated dispatch reads the optional body only when one is present.
        using (Stj.JsonDocument approved = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/approve", Auth, "boss")))
        {
            approved.RootElement.GetProperty("status").GetString().ShouldBe("Approved");
            approved.RootElement.GetProperty("decidedBy").GetString().ShouldBe("boss");
            approved.RootElement.TryGetProperty("grantedUntil", out _).ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task The_request_queue_keyset_pages_over_http()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "boss");

        var submitted = new List<string>();
        foreach (string who in new[] { "alice", "bob", "carol" })
        {
            using Stj.JsonDocument s = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, who));
            submitted.Add(s.RootElement.GetProperty("id").GetString()!);
        }

        // First page (limit 2): two of the three, oldest-first by (createdAt, id), plus a continuation token.
        var seen = new List<string>();
        string token;
        using (Stj.JsonDocument page1 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests?baseWorkflowId=flow&limit=2", Auth, "boss")))
        {
            List<string> ids = page1.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()!).ToList();
            ids.Count.ShouldBe(2);
            seen.AddRange(ids);
            token = page1.RootElement.GetProperty("nextPageToken").GetString()!;
            token.ShouldNotBeNullOrEmpty();
        }

        // Following the token returns the remainder; the last page omits nextPageToken.
        using (Stj.JsonDocument page2 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/accessRequests?baseWorkflowId=flow&limit=2&pageToken={token}", Auth, "boss")))
        {
            List<string> ids = page2.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()!).ToList();
            ids.Count.ShouldBe(1);
            seen.AddRange(ids);
            page2.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse();
        }

        // No gaps or duplicates across the page boundary — every submitted request appears exactly once.
        seen.OrderBy(x => x, StringComparer.Ordinal).ShouldBe(submitted.OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    [TestMethod]
    public async Task A_non_administrator_cannot_approve()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "boss");

        string id;
        using (Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, "alice")))
        {
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // alice is not an administrator of 'flow' → 403; and listing the queue she does not administer → 403.
        (await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/approve", Auth, "alice")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Get, "/accessRequests?baseWorkflowId=flow", Auth, "alice")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_requester_lists_and_withdraws_their_own_request()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "boss");

        string id;
        using (Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, "alice")))
        {
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // alice lists her own requests (no baseWorkflowId).
        using (Stj.JsonDocument mine = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests", Auth, "alice")))
        {
            mine.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain(id);
        }

        // bob (a different requester) cannot withdraw alice's request → 403.
        (await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/withdraw", Auth, "bob")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // alice withdraws her own (bodyless — the optional decision note is omitted).
        using (Stj.JsonDocument withdrawn = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/withdraw", Auth, "alice")))
        {
            withdrawn.RootElement.GetProperty("status").GetString().ShouldBe("Withdrawn");
        }
    }

    [TestMethod]
    public async Task Submitting_without_authentication_is_unauthorized()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", null, "alice"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task An_administrator_grants_eligibility_then_the_requester_self_elevates()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "boss");

        // alice requests run access; it is pending (she is not eligible to self-elevate by claims).
        string id;
        using (Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, "alice")))
        {
            submitted.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // alice (not an administrator) cannot grant eligibility → 403.
        (await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/approve-as-eligible", Auth, "alice")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // boss grants durable eligibility (no live grant) — a bodyless POST (the window is optional).
        using (Stj.JsonDocument eligible = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/accessRequests/{id}/approve-as-eligible", Auth, "boss")))
        {
            eligible.RootElement.GetProperty("status").GetString().ShouldBe("Eligible");
            eligible.RootElement.GetProperty("decidedBy").GetString().ShouldBe("boss");
            eligible.RootElement.TryGetProperty("grantedBindingId", out _).ShouldBeTrue();
        }

        // alice now self-elevates: a fresh request is auto-approved against the stored eligibility — no human approver.
        using (Stj.JsonDocument activated = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", """{"baseWorkflowId":"flow","requestedScopes":["runs:write"]}""", Auth, "alice")))
        {
            activated.RootElement.GetProperty("status").GetString().ShouldBe("Approved");
            activated.RootElement.TryGetProperty("grantedUntil", out _).ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task The_approver_inbox_lists_requests_across_every_administered_workflow()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow-a", "boss");
        await EstablishAsync(host.Catalog, "flow-b", "boss");
        await EstablishAsync(host.Catalog, "flow-c", "carol"); // boss does NOT administer this one

        string aId = await SubmitAsync(host, "flow-a", "alice");
        string bId = await SubmitAsync(host, "flow-b", "dave");
        string cId = await SubmitAsync(host, "flow-c", "eve");

        // boss's inbox (scope=queue, no baseWorkflowId): every request across the workflows boss administers (flow-a +
        // flow-b), regardless of submitter — but never flow-c, which boss does not administer.
        using (Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests?scope=queue", Auth, "boss")))
        {
            var ids = inbox.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
            ids.ShouldContain(aId);
            ids.ShouldContain(bId);
            ids.ShouldNotContain(cId);
        }

        // carol's inbox is just flow-c's request.
        using (Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests?scope=queue", Auth, "carol")))
        {
            inbox.RootElement.GetProperty("accessRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldBe([cId]);
        }

        // A caller who administers nothing gets an empty inbox (not a 403).
        using (Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/accessRequests?scope=queue", Auth, "nobody")))
        {
            inbox.RootElement.GetProperty("accessRequests").EnumerateArray().Count().ShouldBe(0);
        }
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<string> SubmitAsync(Scoped host, string workflowId, string who)
    {
        using Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/accessRequests", $$"""{"baseWorkflowId":"{{workflowId}}","requestedScopes":["runs:write"]}""", Auth, who));
        return submitted.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task EstablishAsync(SecuredWorkflowCatalog catalog, string workflowId, string founder)
    {
        SecurityTagSet founderIdentity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", founder)]);
        await catalog.AddAsync(Package(workflowId), new CatalogOwner("Team", "team@example.com", null, null), default, founderIdentity, default);
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

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        // The test identity (the 'tenant' claim) doubles as the requesting subject, so an approval keys the grant on it.
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), accessRequestSubjectClaimType: "tenant");
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog) : IAsyncDisposable
    {
        public SecuredWorkflowCatalog Catalog => catalog;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope, identity);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope, identity);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope, string? identity)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
                }

                if (identity is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.IdentityHeader, identity);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";
        public const string IdentityHeader = "X-Identity";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            if (this.Request.Headers.TryGetValue(IdentityHeader, out Microsoft.Extensions.Primitives.StringValues who) && !string.IsNullOrEmpty(who.ToString()))
            {
                identity.AddClaim(new Claim("tenant", who.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}