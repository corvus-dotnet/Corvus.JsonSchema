// <copyright file="OAuth2ClientCredentialsAuthenticationProviderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>Tests the OAuth 2.0 client-credentials provider's token caching, proactive refresh, and single-flight.</summary>
[TestClass]
public sealed class OAuth2ClientCredentialsAuthenticationProviderTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Caches_the_token_within_its_lifetime_and_refreshes_proactively_after_expiry()
    {
        var clock = new TestClock(Start);
        var handler = new MockHttpHandler(HttpStatusCode.OK, "{\"access_token\":\"tok-1\",\"expires_in\":3600}");
        using var client = new HttpClient(handler);
        using var provider = new OAuth2ClientCredentialsAuthenticationProvider(client, Options(), clock);

        await ApplyAsync(provider); // first fetch
        await ApplyAsync(provider); // within lifetime → cached
        handler.RequestCount.ShouldBe(1);

        // Within the 60s refresh skew of the 3600s expiry → proactive refresh.
        clock.Advance(TimeSpan.FromSeconds(3550));
        string? token = await ApplyAsync(provider);
        handler.RequestCount.ShouldBe(2);
        token.ShouldBe("tok-1"); // the stub returns the same token; the point is a second fetch occurred
    }

    [TestMethod]
    public async Task Concurrent_first_use_fetches_the_token_once_single_flight()
    {
        var clock = new TestClock(Start);
        var handler = new MockHttpHandler(HttpStatusCode.OK, "{\"access_token\":\"tok-1\",\"expires_in\":3600}");
        var release = new TaskCompletionSource();
        handler.OnRequest = async _ => await release.Task;
        using var client = new HttpClient(handler);
        using var provider = new OAuth2ClientCredentialsAuthenticationProvider(client, Options(), clock);

        Task<string?>[] callers = Enumerable.Range(0, 10).Select(_ => ApplyAsync(provider)).ToArray();
        await Task.Delay(50); // let all callers reach the single-flight gate
        release.SetResult();
        await Task.WhenAll(callers);

        handler.RequestCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task Basic_client_authentication_sends_no_client_secret_in_the_body()
    {
        var clock = new TestClock(Start);
        var handler = new MockHttpHandler(HttpStatusCode.OK, "{\"access_token\":\"tok\",\"expires_in\":3600}");
        using var client = new HttpClient(handler);
        using var provider = new OAuth2ClientCredentialsAuthenticationProvider(
            client,
            new OAuth2ClientCredentialsOptions
            {
                TokenEndpoint = new Uri("https://auth.example/token"),
                ClientId = "client-1",
                ClientSecret = "shh",
                ClientAuthentication = OAuth2ClientAuthentication.BasicAuthenticationHeader,
            },
            clock);

        HttpRequestMessage? captured = null;
        handler.OnRequest = r => { captured = r; return Task.CompletedTask; };
        await ApplyAsync(provider);

        captured!.Headers.Authorization!.Scheme.ShouldBe("Basic");
        handler.RequestBodies[0].ShouldNotContain("client_secret");
    }

    [TestMethod]
    public async Task A_non_success_token_response_throws()
    {
        var clock = new TestClock(Start);
        var handler = new MockHttpHandler(HttpStatusCode.Unauthorized, "nope");
        using var client = new HttpClient(handler);
        using var provider = new OAuth2ClientCredentialsAuthenticationProvider(client, Options(), clock);

        await Should.ThrowAsync<OAuth2TokenException>(async () => await ApplyAsync(provider));
    }

    private static OAuth2ClientCredentialsOptions Options() => new()
    {
        TokenEndpoint = new Uri("https://auth.example/token"),
        ClientId = "client-1",
        ClientSecret = "shh",
        Scope = "read",
    };

    private static async Task<string?> ApplyAsync(OAuth2ClientCredentialsAuthenticationProvider provider)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        return request.Headers.Authorization?.Parameter;
    }
}