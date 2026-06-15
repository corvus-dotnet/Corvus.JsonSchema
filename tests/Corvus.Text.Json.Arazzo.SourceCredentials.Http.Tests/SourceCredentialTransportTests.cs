// <copyright file="SourceCredentialTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
        await f.Store.UpdateAsync("petstore", "production", ApiKey("petstore", "production", "petstore-production-rotated"), etag, "bob", default);
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
    public void CreateApiSources_builds_a_transport_factory_per_source()
    {
        Fixture f = NewFixture();
        using var petsClient = new HttpClient { BaseAddress = new Uri("https://petstore.example/") };
        using var billingClient = new HttpClient { BaseAddress = new Uri("https://billing.example/") };
        var sourceClients = new Dictionary<string, HttpClient> { ["petstore"] = petsClient, ["billing"] = billingClient };

        IReadOnlyDictionary<string, IApiTransportFactory> sources = SourceCredentialTransports.CreateApiSources(sourceClients, "production", f.Cache);

        sources.Keys.OrderBy(k => k).ShouldBe(["billing", "petstore"]);
        foreach (IApiTransportFactory factory in sources.Values)
        {
            IApiTransport transport = factory.CreateTransport();
            transport.ShouldNotBeNull();
        }

        f.Cache.Dispose();
    }

    private static string? Header(HttpRequestMessage request, string name)
        => request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.Single() : null;

    private static SourceCredentialDefinition ApiKey(string sourceName, string environment, string envVar) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"env://{envVar}")],
        [new CredentialConfigDefinition("parameterName", "X-Api-Key")]);

    private static Fixture NewFixture()
    {
        var clock = new TestClock(Start);
        var resolver = new FakeSecretResolver(new()
        {
            ["env://petstore-production"] = "key-v1",
            ["env://petstore-production-rotated"] = "key-v2",
        });
        var store = new InMemorySourceCredentialStore(clock);
        var factory = new SourceCredentialProviderFactory(resolver, timeProvider: clock);
        var cache = new SourceCredentialCache(store, factory, clock, TimeSpan.FromMinutes(5));
        return new Fixture(clock, store, cache);
    }

    private sealed record Fixture(TestClock Clock, InMemorySourceCredentialStore Store, SourceCredentialCache Cache);
}