// <copyright file="SourceCredentialTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>Tests the per-source <see cref="SourceCredentialAuthenticationProvider"/> and the
/// <see cref="SourceCredentialTransports"/> factory composition — the §13 transport wiring.</summary>
[TestClass]
public sealed class SourceCredentialTransportTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task The_provider_applies_the_sources_cached_credential_to_a_request()
    {
        Fixture f = NewFixture();
        await f.Store.AddAsync(ApiKey("petstore", "production", "petstore-production"), "alice", default);
        var provider = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production");

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/pets");
        await provider.AuthenticateAsync(request, default);

        Header(request, "X-Api-Key").ShouldBe("key-v1");
        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_rotation_after_bind_flows_through_on_the_next_request()
    {
        Fixture f = NewFixture();
        ParsedJsonDocument<SourceCredentialBinding> added = await f.Store.AddAsync(ApiKey("petstore", "production", "petstore-production"), "alice", default);
        WorkflowEtag etag = added.RootElement.EtagValue;
        added.Dispose();

        // The provider is bound once (as a run's transport would be).
        var provider = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production");

        using (var first = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/pets"))
        {
            await provider.AuthenticateAsync(first, default);
            Header(first, "X-Api-Key").ShouldBe("key-v1");
        }

        // Rotate the reference in the store, then let the cache TTL lapse — the same bound provider must pick up the
        // new credential on the next request (the property a resumed run relies on, §13.3).
        await f.Store.UpdateAsync("petstore", "production", ApiKey("petstore", "production", "petstore-production-rotated"), etag, "bob", AccessContext.System, default);
        f.Clock.Advance(TimeSpan.FromMinutes(6));

        using (var second = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/pets"))
        {
            await provider.AuthenticateAsync(second, default);
            Header(second, "X-Api-Key").ShouldBe("key-v2");
        }

        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_source_with_no_binding_is_left_unauthenticated()
    {
        Fixture f = NewFixture();
        var provider = new SourceCredentialAuthenticationProvider(f.Cache, "unbound", "production");

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://unbound.example/");
        await provider.AuthenticateAsync(request, default);

        request.Headers.Authorization.ShouldBeNull();
        request.Headers.Contains("X-Api-Key").ShouldBeFalse();
        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_run_authenticates_only_with_the_binding_it_is_entitled_to()
    {
        Fixture f = NewFixture();
        await f.Store.AddAsync(TenantApiKey("petstore", "production", "acme"), "system", default);
        await f.Store.AddAsync(TenantApiKey("petstore", "production", "globex"), "system", default);

        // An acme run authenticates with acme's binding.
        var acme = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/"))
        {
            await acme.AuthenticateAsync(request, default);
            Header(request, "X-Api-Key").ShouldBe("acme-key");
        }

        // A globex run authenticates with globex's binding — never acme's.
        var globex = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/"))
        {
            await globex.AuthenticateAsync(request, default);
            Header(request, "X-Api-Key").ShouldBe("globex-key");
        }

        // A run in neither tenant is entitled to no binding → left unauthenticated (fail closed).
        var orphan = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "initech")]));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/"))
        {
            await orphan.AuthenticateAsync(request, default);
            request.Headers.Contains("X-Api-Key").ShouldBeFalse();
        }

        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_credential_granted_to_a_workflow_identity_is_used_only_by_that_workflows_runs()
    {
        Fixture f = NewFixture();

        // The binding is granted to the workflow "nightly-reconcile" by its immutable identity (sys:workflow) — the
        // unforgeable tag a run inherits from its catalogued version, never one the author sets.
        await f.Store.AddAsync(
            new SourceCredentialDefinition(
                "petstore",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "env://petstore-nightly")],
                [new CredentialConfigDefinition("parameterName", "X-Api-Key")],
                UsageTags: SecurityTagSet.FromTags([WorkflowIdentity.WorkflowTag("nightly-reconcile")])),
            "system",
            default);

        // A run of nightly-reconcile carries sys:workflow=nightly-reconcile → entitled.
        var entitled = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production", SecurityTagSet.FromTags([WorkflowIdentity.WorkflowTag("nightly-reconcile")]));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/"))
        {
            await entitled.AuthenticateAsync(request, default);
            Header(request, "X-Api-Key").ShouldBe("nightly-key");
        }

        // A run of a different workflow carries a different sys:workflow → not entitled → unauthenticated (fail closed).
        // It cannot forge the identity: the tag is stamped from its own catalogued version, not the binding's grant.
        var other = new SourceCredentialAuthenticationProvider(f.Cache, "petstore", "production", SecurityTagSet.FromTags([WorkflowIdentity.WorkflowTag("daily-export")]));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://petstore.example/"))
        {
            await other.AuthenticateAsync(request, default);
            request.Headers.Contains("X-Api-Key").ShouldBeFalse();
        }

        f.Cache.Dispose();
    }

    [TestMethod]
    public void CreateBinder_binds_a_transport_per_declared_source()
    {
        Fixture f = NewFixture();
        using var petsClient = new HttpClient { BaseAddress = new Uri("https://petstore.example/") };
        using var billingClient = new HttpClient { BaseAddress = new Uri("https://billing.example/") };
        var sourceClients = new Dictionary<string, HttpClient> { ["petstore"] = petsClient, ["billing"] = billingClient };

        WorkflowTransportBinder binder = SourceCredentialTransports.CreateBinder(sourceClients, "production", f.Cache);
        WorkflowTransports transports = binder(new WorkflowDescriptor("orders-v1", NeedsMessageTransport: false, ["petstore", "billing"]), default);

        transports.ApiTransports.Keys.OrderBy(k => k).ShouldBe(["billing", "petstore"]);
        foreach (IApiTransport transport in transports.ApiTransports.Values)
        {
            transport.ShouldNotBeNull();
        }

        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_binding_base_url_override_sends_the_request_to_that_environments_endpoint()
    {
        Fixture f = NewFixture();

        // The binding carries a per-environment base URL override (§8) in its non-secret config.
        await f.Store.AddAsync(ApiKeyWithBaseUrl("petstore", "production", "petstore-production", "https://staging.petstore.example/"), "alice", default);
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://default.petstore.example/") };

        IApiTransport transport = SourceCredentialTransports.CreateApiTransportFactory(client, "petstore", "production", f.Cache).CreateTransport();
        await transport.SendAsync<FakeRequest, FakeResponse>(default, default);

        handler.LastRequestUri?.ToString().ShouldBe("https://staging.petstore.example/x");
        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task Without_a_base_url_override_the_request_uses_the_clients_base_address()
    {
        Fixture f = NewFixture();
        await f.Store.AddAsync(ApiKey("petstore", "production", "petstore-production"), "alice", default);
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://default.petstore.example/") };

        IApiTransport transport = SourceCredentialTransports.CreateApiTransportFactory(client, "petstore", "production", f.Cache).CreateTransport();
        await transport.SendAsync<FakeRequest, FakeResponse>(default, default);

        handler.LastRequestUri?.ToString().ShouldBe("https://default.petstore.example/x");
        f.Cache.Dispose();
    }

    private static string? Header(HttpRequestMessage request, string name)
        => request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.Single() : null;

    private static SourceCredentialDefinition ApiKeyWithBaseUrl(string sourceName, string environment, string envVar, string baseUrl) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"env://{envVar}")],
        [new CredentialConfigDefinition("parameterName", "X-Api-Key"), new CredentialConfigDefinition("baseUrl", baseUrl)]);

    private static SourceCredentialDefinition ApiKey(string sourceName, string environment, string envVar) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"env://{envVar}")],
        [new CredentialConfigDefinition("parameterName", "X-Api-Key")]);

    private static SourceCredentialDefinition TenantApiKey(string sourceName, string environment, string tenant) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"env://{sourceName}-{tenant}")],
        [new CredentialConfigDefinition("parameterName", "X-Api-Key")],
        UsageTags: SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]));

    [TestMethod]
    public async Task A_401_on_an_authenticated_source_faults_as_credentials_expired()
    {
        Fixture f = NewFixture();
        await f.Store.AddAsync(ApiKey("petstore", "production", "petstore-production"), "alice", default);
        var transport = new SourceCredentialApiTransport(new FixedStatusTransport(401), f.Cache, "petstore", "production");

        SourceCredentialExpiredException ex = await Should.ThrowAsync<SourceCredentialExpiredException>(
            async () => await transport.SendAsync<FakeRequest, FakeResponse>(default, default));

        ex.SourceName.ShouldBe("petstore");
        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_success_response_passes_through_unchanged()
    {
        Fixture f = NewFixture();
        await f.Store.AddAsync(ApiKey("petstore", "production", "petstore-production"), "alice", default);
        var transport = new SourceCredentialApiTransport(new FixedStatusTransport(200), f.Cache, "petstore", "production");

        FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(default, default);

        response.StatusCode.ShouldBe(200);
        f.Cache.Dispose();
    }

    [TestMethod]
    public async Task A_403_on_an_unauthenticated_source_is_not_a_credential_fault()
    {
        Fixture f = NewFixture();

        // No binding for "unbound" → the run is not entitled to a credential, so the 403 is left to ordinary step
        // criteria rather than mislabelled a credential rejection.
        var transport = new SourceCredentialApiTransport(new FixedStatusTransport(403), f.Cache, "unbound", "production");

        FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(default, default);

        response.StatusCode.ShouldBe(403);
        f.Cache.Dispose();
    }

    private static Fixture NewFixture()
    {
        var clock = new TestClock(Start);
        var resolver = new FakeSecretResolver(new()
        {
            ["env://petstore-production"] = "key-v1",
            ["env://petstore-production-rotated"] = "key-v2",
            ["env://petstore-acme"] = "acme-key",
            ["env://petstore-globex"] = "globex-key",
            ["env://petstore-nightly"] = "nightly-key",
        });
        var store = new InMemorySourceCredentialStore(clock);
        var factory = new SourceCredentialProviderFactory(resolver, timeProvider: clock);
        var cache = new SourceCredentialCache(store, factory, clock, TimeSpan.FromMinutes(5));
        return new Fixture(clock, store, cache);
    }

    private sealed record Fixture(TestClock Clock, InMemorySourceCredentialStore Store, SourceCredentialCache Cache);

    // Captures the absolute request URI HttpClient sends (after BaseAddress / override resolution) and returns 200.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    // An inner transport that returns a fixed HTTP status for every call — drives the decorator's response check.
    private sealed class FixedStatusTransport(int statusCode) : IApiTransport
    {
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => TResponse.CreateAsync(statusCode, System.IO.Stream.Null, cancellationToken: cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, Corvus.Text.Json.Internal.IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => TResponse.CreateAsync(statusCode, System.IO.Stream.Null, cancellationToken: cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, System.IO.Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => TResponse.CreateAsync(statusCode, System.IO.Stream.Null, cancellationToken: cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<System.IO.Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => TResponse.CreateAsync(statusCode, System.IO.Stream.Null, cancellationToken: cancellationToken);

        public ValueTask DisposeAsync() => default;
    }

    private readonly struct FakeRequest : IApiRequest<FakeRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/x"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(System.Buffers.IBufferWriter<byte> writer)
        {
        }

        public int WriteQueryString(System.Buffers.IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
        {
        }

        public int WriteCookies(System.Buffers.IBufferWriter<byte> writer) => 0;

        public void Validate(ValidationMode mode = ValidationMode.Basic)
        {
        }
    }

    private readonly struct FakeResponse(int statusCode) : IApiResponse<FakeResponse>
    {
        public int StatusCode => statusCode;

        public bool IsSuccess => statusCode is >= 200 and < 300;

        public static ValueTask<FakeResponse> CreateAsync(int statusCode, System.IO.Stream contentStream, string? contentType = null, IResponseHeaders? responseHeaders = null, IAsyncDisposable? owner = null, IApiTransport? transport = null, CancellationToken cancellationToken = default)
            => new(new FakeResponse(statusCode));

        public void Validate(ValidationMode mode = ValidationMode.Basic)
        {
        }

        public ValueTask DisposeAsync() => default;
    }
}