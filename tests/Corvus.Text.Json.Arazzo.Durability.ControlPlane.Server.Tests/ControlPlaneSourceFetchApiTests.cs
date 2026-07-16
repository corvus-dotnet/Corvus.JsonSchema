// <copyright file="ControlPlaneSourceFetchApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
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
/// Tests <c>fetchSourceDocument</c> (workflow-designer design §4.4): server-side fetch of
/// OpenAPI/AsyncAPI documents over a stubbed outbound client — JSON and YAML payloads, type/version
/// detection, the canonical digest (key order does not change it), the https-only scheme guard, the
/// upstream failure and size-cap mappings to 502, the credentialed fetch applying a registered
/// bearer credential resolved through the env secret scheme, and the fails-closed 400 when a
/// deployment wires no fetcher.
/// </summary>
[TestClass]
public sealed class ControlPlaneSourceFetchApiTests
{
    private const string Read = "sources:read";

    private const string PetstoreJson =
        """{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0"},"paths":{"/pets":{"get":{"operationId":"listPets","responses":{"200":{"description":"ok"}}}}}}""";

    private const string PetstoreJsonReordered =
        """{"paths":{"/pets":{"get":{"responses":{"200":{"description":"ok"}},"operationId":"listPets"}}},"info":{"version":"1.0","title":"Petstore"},"openapi":"3.1.0"}""";

    private const string PetstoreYaml =
        """
        openapi: "3.1.0"
        info:
          title: Petstore
          version: "1.0"
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                "200":
                  description: ok
        """;

    [TestMethod]
    public async Task A_json_document_fetches_with_detected_type_version_and_digest()
    {
        await using Scoped host = await StartAsync(stub => stub.Map("https://specs.example/petstore.json", 200, "application/json", PetstoreJson));

        using Stj.JsonDocument fetched = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/petstore.json"}""", Read));
        fetched.RootElement.GetProperty("url").GetString().ShouldBe("https://specs.example/petstore.json");
        fetched.RootElement.GetProperty("type").GetString().ShouldBe("openapi");
        fetched.RootElement.GetProperty("version").GetString().ShouldBe("3.1.0");
        fetched.RootElement.GetProperty("contentType").GetString().ShouldBe("application/json");
        fetched.RootElement.GetProperty("digest").GetString()!.Length.ShouldBe(64);
        fetched.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Petstore");
    }

    [TestMethod]
    public async Task A_yaml_document_parses_to_the_same_json_form_and_digest()
    {
        await using Scoped host = await StartAsync(stub =>
        {
            stub.Map("https://specs.example/petstore.json", 200, "application/json", PetstoreJson);
            stub.Map("https://specs.example/petstore.yaml", 200, "application/yaml", PetstoreYaml);
            stub.Map("https://specs.example/petstore-reordered.json", 200, "application/json", PetstoreJsonReordered);
        });

        string jsonDigest;
        using (Stj.JsonDocument json = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/petstore.json"}""", Read)))
        {
            jsonDigest = json.RootElement.GetProperty("digest").GetString()!;
        }

        // YAML parses to the same document → the same canonical digest.
        using (Stj.JsonDocument yaml = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/petstore.yaml"}""", Read)))
        {
            yaml.RootElement.GetProperty("type").GetString().ShouldBe("openapi");
            yaml.RootElement.GetProperty("document").GetProperty("paths").GetProperty("/pets").GetProperty("get").GetProperty("operationId").GetString().ShouldBe("listPets");
            yaml.RootElement.GetProperty("digest").GetString().ShouldBe(jsonDigest, "the digest is over the RFC 8785 canonical form");
        }

        // Key order does not change the canonical digest either.
        using Stj.JsonDocument reordered = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/petstore-reordered.json"}""", Read));
        reordered.RootElement.GetProperty("digest").GetString().ShouldBe(jsonDigest);
    }

    [TestMethod]
    public async Task The_scheme_guard_and_payload_validation_reject_bad_fetches()
    {
        await using Scoped host = await StartAsync(stub =>
        {
            stub.Map("https://specs.example/not-json.txt", 200, "text/plain", ": not a document : [");
            stub.Map("https://specs.example/typeless.json", 200, "application/json", """{"hello":"world"}""");
            stub.Map("https://specs.example/error.json", 500, "application/json", "{}");
            stub.Map("https://specs.example/huge.json", 200, "application/json", "{\"openapi\":\"3.1.0\",\"pad\":\"" + new string('x', 64 * 1024) + "\"}");
        });

        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"http://specs.example/petstore.json"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // https only by default
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"ftp://specs.example/x"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"not a url"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/not-json.txt"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // neither JSON nor YAML... (YAML is permissive; see below)
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/typeless.json"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // parses, but declares no recognisable type
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/error.json"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/huge.json"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadGateway); // over the (test-sized) cap
    }

    [TestMethod]
    public async Task A_credentialed_fetch_applies_the_registered_bearer_credential()
    {
        System.Environment.SetEnvironmentVariable("ARAZZO_FETCH_TEST_TOKEN", "sekret-token");
        try
        {
            StubHttpHandler? stubRef = null;
            await using Scoped host = await StartAsync(stub =>
            {
                stubRef = stub;
                stub.Map("https://specs.example/private.json", 200, "application/json", PetstoreJson);
            });

            // Register the credential through the public surface, then fetch with its reference.
            (await host.SendJsonAsync(
                HttpMethod.Post, "/credentials",
                """{"sourceName":"petstore","environment":"production","authKind":"bearer","secretRefs":[{"name":"value","ref":"env://ARAZZO_FETCH_TEST_TOKEN"}]}""",
                "credentials:write")).StatusCode.ShouldBe(HttpStatusCode.Created);

            (await host.SendJsonAsync(
                HttpMethod.Post, "/sources/fetch",
                """{"url":"https://specs.example/private.json","credential":{"sourceName":"petstore","environment":"production"}}""",
                Read)).StatusCode.ShouldBe(HttpStatusCode.OK);
            stubRef!.LastAuthorization.ShouldBe("Bearer sekret-token");

            // An unknown reference is non-disclosing.
            (await host.SendJsonAsync(
                HttpMethod.Post, "/sources/fetch",
                """{"url":"https://specs.example/private.json","credential":{"sourceName":"nope","environment":"production"}}""",
                Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("ARAZZO_FETCH_TEST_TOKEN", null);
        }
    }

    [TestMethod]
    public async Task Fetching_fails_closed_when_the_deployment_wires_no_fetcher()
    {
        await using Scoped host = await StartAsync(configureStub: null);
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", """{"url":"https://specs.example/petstore.json"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Scoped> StartAsync(Action<StubHttpHandler>? configureStub)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");
        var credentialStore = new InMemorySourceCredentialStore();

        SourceDocumentFetcher? fetcher = null;
        if (configureStub is not null)
        {
            var stub = new StubHttpHandler();
            configureStub(stub);
            var providerFactory = new SourceCredentialProviderFactory(new SecretResolverBuilder().AddEnvironment().Build());
            fetcher = new SourceDocumentFetcher(new HttpClient(stub), credentialStore, providerFactory, maxDocumentBytes: 32 * 1024);
        }

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
        app.MapArazzoControlPlane(
            management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly,
            sourceCredentialStore: credentialStore, sourceFetcher: fetcher);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    /// <summary>A canned outbound endpoint: URL → (status, content type, body), recording the last Authorization header.</summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (int Status, string ContentType, string Body)> responses = [];

        public string? LastAuthorization { get; private set; }

        public void Map(string url, int status, string contentType, string body) => this.responses[url] = (status, contentType, body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastAuthorization = request.Headers.Authorization?.ToString();
            if (!this.responses.TryGetValue(request.RequestUri!.ToString(), out (int Status, string ContentType, string Body) canned))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var response = new HttpResponseMessage((HttpStatusCode)canned.Status)
            {
                Content = new StringContent(canned.Body, Encoding.UTF8, canned.ContentType),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
        {
            var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (scope is not null)
            {
                request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
            }

            return this.SendCoreAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request)
        {
            using (request)
            {
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