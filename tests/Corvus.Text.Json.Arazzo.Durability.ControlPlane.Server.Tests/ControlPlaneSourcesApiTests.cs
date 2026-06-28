// <copyright file="ControlPlaneSourcesApiTests.cs" company="Endjin Limited">
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
/// Tests the control-plane sources registry API (§7.6): first-class, reach-scoped source CRUD over <c>/sources</c>, gated
/// by the <c>sources:read</c>/<c>sources:write</c> scopes. Sources are reach-scoped, not governed — there is no
/// administrator set; the document is returned only on a single read, never in the list.
/// </summary>
[TestClass]
public sealed class ControlPlaneSourcesApiTests
{
    private const string Write = "sources:write";
    private const string Read = "sources:read";

    private const string PetstoreBody =
        """{"name":"petstore","type":"openapi","document":{"openapi":"3.1.0","info":{"title":"Petstore"}},"displayName":"Pet Store","description":"The pet store API."}""";

    [TestMethod]
    public async Task A_source_has_a_full_register_get_list_update_delete_lifecycle()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            doc.RootElement.GetProperty("name").GetString().ShouldBe("petstore");
            doc.RootElement.GetProperty("type").GetString().ShouldBe("openapi");
            doc.RootElement.GetProperty("displayName").GetString().ShouldBe("Pet Store");
            doc.RootElement.GetProperty("createdBy").GetString().ShouldNotBeNullOrEmpty();
            doc.RootElement.GetProperty("etag").GetString().ShouldNotBeNullOrEmpty();

            // The register response is the full source — the document round-trips verbatim.
            doc.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Petstore");
        }

        (await host.SendAsync(HttpMethod.Get, "/sources/petstore", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/sources", Read)))
        {
            list.RootElement.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["petstore"]);
        }

        HttpResponseMessage updated = await host.SendJsonAsync(
            HttpMethod.Put,
            "/sources/petstore",
            """{"displayName":"Pet Store (v2)","description":"Updated."}""",
            Write);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(updated))
        {
            doc.RootElement.GetProperty("displayName").GetString().ShouldBe("Pet Store (v2)");
            doc.RootElement.GetProperty("description").GetString().ShouldBe("Updated.");
            doc.RootElement.GetProperty("lastUpdatedBy").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Delete, "/sources/petstore", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, "/sources/petstore", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_list_omits_the_document_but_a_single_read_includes_it()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A single read carries the full document.
        using (Stj.JsonDocument single = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/sources/petstore", Read)))
        {
            single.RootElement.TryGetProperty("document", out _).ShouldBeTrue();
        }

        // The list entry is a document-less summary (the design's key projection decision).
        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/sources", Read)))
        {
            Stj.JsonElement entry = list.RootElement.GetProperty("sources").EnumerateArray().Single();
            entry.GetProperty("name").GetString().ShouldBe("petstore");
            entry.GetProperty("type").GetString().ShouldBe("openapi");
            entry.TryGetProperty("document", out _).ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task An_update_omitting_the_document_keeps_it_and_supplying_one_rotates_it()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A metadata-only update keeps the stored document.
        using (Stj.JsonDocument renamed = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/sources/petstore", """{"displayName":"Renamed"}""", Write)))
        {
            renamed.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Petstore");
        }

        // Supplying a document rotates it.
        using (Stj.JsonDocument rotated = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Put,
            "/sources/petstore",
            """{"document":{"openapi":"3.1.0","info":{"title":"Petstore v2"}}}""",
            Write)))
        {
            rotated.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Petstore v2");
        }
    }

    [TestMethod]
    public async Task Registering_a_duplicate_source_conflicts()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Write)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Getting_a_missing_source_is_not_found()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendAsync(HttpMethod.Get, "/sources/nope", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/sources", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/sources", PetstoreBody, Read)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/sources", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(ControlPlaneRowSecurityPolicy? rowSecurity = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), rowSecurity is null ? ControlPlaneSecurityMode.ScopesOnly : ControlPlaneSecurityMode.Scoped, rowSecurity: rowSecurity);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    /// <summary>A scoped policy giving every caller full reach (so sources are visible) and a fixed deployment identity
    /// <c>sys:tenant=acme</c> (stamped onto registered sources' management tags).</summary>
    private sealed class TenantPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [new SecurityTag("sys:tenant", "acme")];
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}