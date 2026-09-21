// <copyright file="FunctionKeyServerlessInvokeAuthenticatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Tests;

/// <summary>
/// Proves <see cref="FunctionKeyServerlessInvokeAuthenticator"/> presents the invoke key from the runner's secret store
/// in the <c>x-functions-key</c> header, never sends it over plain HTTP to another machine, and holds a resolved key for
/// its cache window and no longer.
/// </summary>
[TestClass]
public sealed class FunctionKeyServerlessInvokeAuthenticatorTests
{
    private static readonly SecretRef KeyRef = SecretRef.Parse("env://ARAZZO_INVOKE_KEY");

    [TestMethod]
    public async Task Presents_the_key_in_the_functions_key_header_and_not_in_the_url()
    {
        var authenticator = new FunctionKeyServerlessInvokeAuthenticator(new CountingResolver("k-1"), KeyRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.azurewebsites.net/api/invoke");

        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.GetValues("x-functions-key").Single().ShouldBe("k-1");
        request.RequestUri!.Query.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Refuses_to_send_the_key_over_plain_http_to_another_machine()
    {
        var resolver = new CountingResolver("k-1");
        var authenticator = new FunctionKeyServerlessInvokeAuthenticator(resolver, KeyRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://app.azurewebsites.net/api/invoke");

        await Should.ThrowAsync<InvalidOperationException>(async () => await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default));

        request.Headers.Contains("x-functions-key").ShouldBeFalse();
        resolver.Resolutions.ShouldBe(0);
    }

    [TestMethod]
    public async Task Sends_the_key_over_plain_http_to_the_loopback_interface()
    {
        var authenticator = new FunctionKeyServerlessInvokeAuthenticator(new CountingResolver("k-1"), KeyRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:7071/api/invoke");

        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.GetValues("x-functions-key").Single().ShouldBe("k-1");
    }

    [TestMethod]
    public async Task Holds_a_resolved_key_for_the_cache_window_then_resolves_again()
    {
        var resolver = new CountingResolver("k-1");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var authenticator = new FunctionKeyServerlessInvokeAuthenticator(resolver, KeyRef, clock) { CacheWindow = TimeSpan.FromMinutes(5) };

        await AuthenticateAsync(authenticator);
        clock.Advance(TimeSpan.FromMinutes(4));
        (await AuthenticateAsync(authenticator)).ShouldBe("k-1");
        resolver.Resolutions.ShouldBe(1);

        // The key is rotated in the secret store. Once the window has passed, the new key is what is presented.
        resolver.Value = "k-2";
        clock.Advance(TimeSpan.FromMinutes(1));
        (await AuthenticateAsync(authenticator)).ShouldBe("k-2");
        resolver.Resolutions.ShouldBe(2);
    }

    [TestMethod]
    public async Task Authenticating_again_replaces_the_header_rather_than_adding_a_second()
    {
        var authenticator = new FunctionKeyServerlessInvokeAuthenticator(new CountingResolver("k-1"), KeyRef);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.azurewebsites.net/api/invoke");

        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);
        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.GetValues("x-functions-key").Count().ShouldBe(1);
    }

    [TestMethod]
    public void Rejects_a_null_resolver()
        => Should.Throw<ArgumentNullException>(() => new FunctionKeyServerlessInvokeAuthenticator(null!, KeyRef));

    private static async Task<string> AuthenticateAsync(FunctionKeyServerlessInvokeAuthenticator authenticator)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.azurewebsites.net/api/invoke");
        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);
        return request.Headers.GetValues("x-functions-key").Single();
    }

    private sealed class CountingResolver(string value) : ISecretResolver
    {
        public string Value { get; set; } = value;

        public int Resolutions { get; private set; }

        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
        {
            this.Resolutions++;
            return ValueTask.FromResult(SecretMaterial.FromString(this.Value));
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}