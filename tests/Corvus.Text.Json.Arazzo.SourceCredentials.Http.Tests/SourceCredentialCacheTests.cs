// <copyright file="SourceCredentialCacheTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>Tests the runner credential cache: warm reuse, TTL refresh with etag invalidation, negative caching, and
/// explicit invalidation.</summary>
[TestClass]
public sealed class SourceCredentialCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_warm_hit_reuses_the_provider_without_re_resolving_the_secret()
    {
        (SourceCredentialCache cache, FakeSecretResolver resolver, InMemorySourceCredentialStore store, TestClock clock) = NewCache();
        await store.AddAsync(ApiKeyDefinition("petstore", "production"), "alice", default);

        IHttpAuthenticationProvider? first = await cache.GetAsync("petstore", "production", default);
        IHttpAuthenticationProvider? second = await cache.GetAsync("petstore", "production", default);

        first.ShouldNotBeNull();
        second.ShouldBeSameAs(first); // warm hit, same instance
        resolver.ResolveCount.ShouldBe(1); // resolved once, not per get
        cache.Dispose();
    }

    [TestMethod]
    public async Task After_ttl_an_unchanged_binding_keeps_the_provider_a_changed_one_rebuilds()
    {
        (SourceCredentialCache cache, FakeSecretResolver resolver, InMemorySourceCredentialStore store, TestClock clock) = NewCache(ttl: TimeSpan.FromMinutes(5));
        ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(ApiKeyDefinition("petstore", "production"), "alice", default);
        WorkflowEtag etag = added.RootElement.EtagValue;
        added.Dispose();

        IHttpAuthenticationProvider? first = await cache.GetAsync("petstore", "production", default);

        // Past the TTL, but the binding is unchanged → provider kept, secret not re-resolved.
        clock.Advance(TimeSpan.FromMinutes(6));
        IHttpAuthenticationProvider? afterTtlUnchanged = await cache.GetAsync("petstore", "production", default);
        afterTtlUnchanged.ShouldBeSameAs(first);
        resolver.ResolveCount.ShouldBe(1);

        // Rotate the binding (new etag), past the TTL → rebuild + re-resolve.
        await store.UpdateAsync("petstore", "production", ApiKeyDefinition("petstore", "production", "rotated"), etag, "bob", default);
        clock.Advance(TimeSpan.FromMinutes(6));
        IHttpAuthenticationProvider? afterRotation = await cache.GetAsync("petstore", "production", default);
        afterRotation.ShouldNotBeSameAs(first);
        resolver.ResolveCount.ShouldBe(2);
        cache.Dispose();
    }

    [TestMethod]
    public async Task A_source_with_no_binding_returns_null()
    {
        (SourceCredentialCache cache, _, _, _) = NewCache();
        (await cache.GetAsync("absent", "production", default)).ShouldBeNull();
        cache.Dispose();
    }

    [TestMethod]
    public async Task Invalidate_forces_a_rebuild_on_the_next_get()
    {
        (SourceCredentialCache cache, FakeSecretResolver resolver, InMemorySourceCredentialStore store, _) = NewCache();
        await store.AddAsync(ApiKeyDefinition("petstore", "production"), "alice", default);

        IHttpAuthenticationProvider? first = await cache.GetAsync("petstore", "production", default);
        cache.Invalidate("petstore", "production");
        IHttpAuthenticationProvider? second = await cache.GetAsync("petstore", "production", default);

        second.ShouldNotBeSameAs(first);
        resolver.ResolveCount.ShouldBe(2);
        cache.Dispose();
    }

    private static (SourceCredentialCache Cache, FakeSecretResolver Resolver, InMemorySourceCredentialStore Store, TestClock Clock) NewCache(TimeSpan? ttl = null)
    {
        var clock = new TestClock(Start);
        var resolver = new FakeSecretResolver(new()
        {
            ["env://petstore-production"] = "key-v1",
            ["env://petstore-production-rotated"] = "key-v2",
        });
        var store = new InMemorySourceCredentialStore(clock);
        var factory = new SourceCredentialProviderFactory(resolver, timeProvider: clock);
        var cache = new SourceCredentialCache(store, factory, clock, ttl);
        return (cache, resolver, store, clock);
    }

    private static SourceCredentialDefinition ApiKeyDefinition(string sourceName, string environment, string variant = "")
        => new(
            sourceName,
            environment,
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", $"env://{sourceName}-{environment}{(variant.Length == 0 ? string.Empty : "-" + variant)}")],
            [new CredentialConfigDefinition("parameterName", "X-Api-Key")]);
}