// <copyright file="MicroGuestSidecarInvokeAuthenticatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy.Tests;

[TestClass]
public sealed class MicroGuestSidecarInvokeAuthenticatorTests
{
    private static readonly SecretRef TokenRef = SecretRef.Parse("env://ARAZZO_SIDECAR_ADMIN_TOKEN");

    [TestMethod]
    public async Task Presents_the_admin_token_as_a_bearer_on_a_loopback_invoke()
    {
        var authenticator = new MicroGuestSidecarInvokeAuthenticator(new CountingResolver("t-1"), TokenRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:9411/invoke/arazzo-mg-pets-v3");

        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("t-1");
    }

    [TestMethod]
    public async Task Refuses_to_invoke_anything_off_the_loopback_interface_without_reading_the_token()
    {
        var resolver = new CountingResolver("t-1");
        var authenticator = new MicroGuestSidecarInvokeAuthenticator(resolver, TokenRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://172.20.0.10:9411/invoke/arazzo-mg-pets-v3");

        await Should.ThrowAsync<InvalidOperationException>(async () => await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default));

        request.Headers.Authorization.ShouldBeNull();
        resolver.Resolutions.ShouldBe(0);
    }

    [TestMethod]
    public async Task Holds_a_resolved_token_for_the_cache_window_then_resolves_again()
    {
        var resolver = new CountingResolver("t-1");
        var clock = new SettableClock(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        var authenticator = new MicroGuestSidecarInvokeAuthenticator(resolver, TokenRef, clock) { CacheWindow = TimeSpan.FromMinutes(5) };

        using (var first = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:9411/invoke/a"))
        {
            await authenticator.AuthenticateAsync(first, ReadOnlyMemory<byte>.Empty, default);
        }

        clock.Now += TimeSpan.FromMinutes(4);
        using (var second = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:9411/invoke/a"))
        {
            await authenticator.AuthenticateAsync(second, ReadOnlyMemory<byte>.Empty, default);
        }

        resolver.Resolutions.ShouldBe(1);

        clock.Now += TimeSpan.FromMinutes(2);
        using (var third = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:9411/invoke/a"))
        {
            await authenticator.AuthenticateAsync(third, ReadOnlyMemory<byte>.Empty, default);
        }

        resolver.Resolutions.ShouldBe(2);
    }

    private sealed class CountingResolver(string value) : ISecretResolver
    {
        public int Resolutions { get; private set; }

        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
        {
            this.Resolutions++;
            return ValueTask.FromResult(SecretMaterial.FromString(value));
        }
    }

    private sealed class SettableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => this.Now;
    }
}