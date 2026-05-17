// <copyright file="AuthenticationProviderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi.Runtime.Tests;

[TestClass]
public class AuthenticationProviderTests
{
    // ── BearerTokenAuthenticationProvider ──────────────────────────────────
    [TestMethod]
    public async Task BearerToken_StaticToken_SetsAuthorizationHeader()
    {
        BearerTokenAuthenticationProvider provider = new("my-token-123");
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.IsNotNull(request.Headers.Authorization);
        Assert.AreEqual("Bearer", request.Headers.Authorization.Scheme);
        Assert.AreEqual("my-token-123", request.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public async Task BearerToken_TokenFactory_CallsFactoryOnEachRequest()
    {
        int callCount = 0;
        BearerTokenAuthenticationProvider provider = new(ct =>
        {
            callCount++;
            return new ValueTask<string>($"token-{callCount}");
        });

        HttpRequestMessage request1 = new(HttpMethod.Get, "http://localhost/api/items");
        await provider.AuthenticateAsync(request1, CancellationToken.None);
        Assert.AreEqual("token-1", request1.Headers.Authorization!.Parameter);

        HttpRequestMessage request2 = new(HttpMethod.Post, "http://localhost/api/items");
        await provider.AuthenticateAsync(request2, CancellationToken.None);
        Assert.AreEqual("token-2", request2.Headers.Authorization!.Parameter);
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public async Task BearerToken_OverwritesPreviousAuthorizationHeader()
    {
        BearerTokenAuthenticationProvider provider = new("new-token");
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "old-token");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.AreEqual("new-token", request.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public void BearerToken_NullStaticToken_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new BearerTokenAuthenticationProvider((string)null!));
    }

    [TestMethod]
    public void BearerToken_NullFactory_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new BearerTokenAuthenticationProvider((Func<CancellationToken, ValueTask<string>>)null!));
    }

    // ── BasicAuthenticationProvider ───────────────────────────────────────
    [TestMethod]
    public async Task BasicAuth_SetsAuthorizationHeader()
    {
        BasicAuthenticationProvider provider = new("user", "pass");
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.IsNotNull(request.Headers.Authorization);
        Assert.AreEqual("Basic", request.Headers.Authorization.Scheme);

        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"));
        Assert.AreEqual(expected, request.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public async Task BasicAuth_SpecialCharactersInCredentials()
    {
        BasicAuthenticationProvider provider = new("admin@corp", "p@ss:w0rd!");
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin@corp:p@ss:w0rd!"));
        Assert.AreEqual(expected, request.Headers.Authorization!.Parameter);
    }

    [TestMethod]
    public async Task BasicAuth_ReusesSameHeaderValue()
    {
        BasicAuthenticationProvider provider = new("user", "pass");
        HttpRequestMessage request1 = new(HttpMethod.Get, "http://localhost/1");
        HttpRequestMessage request2 = new(HttpMethod.Get, "http://localhost/2");

        await provider.AuthenticateAsync(request1, CancellationToken.None);
        await provider.AuthenticateAsync(request2, CancellationToken.None);

        Assert.AreEqual(
            request1.Headers.Authorization!.Parameter,
            request2.Headers.Authorization!.Parameter);
    }

    [TestMethod]
    public void BasicAuth_NullUsername_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new BasicAuthenticationProvider(null!, "pass"));
    }

    [TestMethod]
    public void BasicAuth_NullPassword_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new BasicAuthenticationProvider("user", null!));
    }

    // ── ApiKeyAuthenticationProvider ──────────────────────────────────────
    [TestMethod]
    public async Task ApiKey_Header_AddsHeaderToRequest()
    {
        ApiKeyAuthenticationProvider provider = new("secret-key", "X-API-Key", ApiKeyLocation.Header);
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.IsTrue(request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? values));
        Assert.AreEqual("secret-key", values!.First());
    }

    [TestMethod]
    public async Task ApiKey_Query_AppendsToUriWithoutExistingQuery()
    {
        ApiKeyAuthenticationProvider provider = new("abc123", "api_key", ApiKeyLocation.Query);
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.AreEqual("http://localhost/api/items?api_key=abc123", request.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task ApiKey_Query_AppendsToUriWithExistingQuery()
    {
        ApiKeyAuthenticationProvider provider = new("abc123", "api_key", ApiKeyLocation.Query);
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items?page=1");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.AreEqual("http://localhost/api/items?page=1&api_key=abc123", request.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task ApiKey_Query_EscapesSpecialCharacters()
    {
        ApiKeyAuthenticationProvider provider = new("key with spaces", "param name", ApiKeyLocation.Query);
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        string uri = request.RequestUri!.OriginalString;
        Assert.IsTrue(uri.Contains("param%20name=key%20with%20spaces"));
    }

    [TestMethod]
    public async Task ApiKey_Cookie_AddsCookieHeader()
    {
        ApiKeyAuthenticationProvider provider = new("session-value", "session_id", ApiKeyLocation.Cookie);
        HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/api/items");

        await provider.AuthenticateAsync(request, CancellationToken.None);

        Assert.IsTrue(request.Headers.TryGetValues("Cookie", out IEnumerable<string>? values));
        Assert.AreEqual("session_id=session-value", values!.First());
    }

    [TestMethod]
    public void ApiKey_NullApiKey_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ApiKeyAuthenticationProvider(null!, "name", ApiKeyLocation.Header));
    }

    [TestMethod]
    public void ApiKey_NullParameterName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ApiKeyAuthenticationProvider("key", null!, ApiKeyLocation.Header));
    }

    // ── HttpClientTransport auth integration ─────────────────────────────
    [TestMethod]
    public async Task Transport_WithAuthProvider_CallsAuthenticateBeforeSending()
    {
        string? capturedAuthHeader = null;
        MockHandler handler = new(
            _ =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"1","name":"test","price":1.0}""", Encoding.UTF8, "application/json"),
                };
            },
            captureAuth: msg =>
            {
                capturedAuthHeader = msg.Headers.Authorization?.ToString();
            });

        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };
        BearerTokenAuthenticationProvider authProvider = new("test-token");
        await using HttpClientTransport transport = new(client, authProvider);

        await using GetItemResponse response = await transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"item-1\""u8)),
                CancellationToken.None);

        Assert.AreEqual("Bearer test-token", capturedAuthHeader);
    }

    [TestMethod]
    public async Task Transport_WithoutAuthProvider_SendsWithoutAuthHeader()
    {
        string? capturedAuthHeader = "not-cleared";
        MockHandler handler = new(
            _ =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"1","name":"test","price":1.0}""", Encoding.UTF8, "application/json"),
                };
            },
            captureAuth: msg =>
            {
                capturedAuthHeader = msg.Headers.Authorization?.ToString();
            });

        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };
        await using HttpClientTransport transport = new(client);

        await using GetItemResponse response = await transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"item-1\""u8)),
                CancellationToken.None);

        Assert.IsNull(capturedAuthHeader);
    }

    [TestMethod]
    public async Task Transport_WithApiKeyAuth_AddsHeaderToRequest()
    {
        string? capturedApiKey = null;
        MockHandler handler = new(
            msg =>
            {
                if (msg.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? values))
                {
                    capturedApiKey = values.First();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"1","name":"test","price":1.0}""", Encoding.UTF8, "application/json"),
                };
            });

        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };
        ApiKeyAuthenticationProvider authProvider = new("my-api-key", "X-API-Key", ApiKeyLocation.Header);
        await using HttpClientTransport transport = new(client, authProvider);

        await using GetItemResponse response = await transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"item-1\""u8)),
                CancellationToken.None);

        Assert.AreEqual("my-api-key", capturedApiKey);
    }

    /// <summary>
    /// Minimal mock <see cref="HttpMessageHandler"/> for auth tests.
    /// </summary>
    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> factory;
        private readonly Action<HttpRequestMessage>? captureAuth;

        public MockHandler(
            Func<HttpRequestMessage, HttpResponseMessage> factory,
            Action<HttpRequestMessage>? captureAuth = null)
        {
            this.factory = factory;
            this.captureAuth = captureAuth;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.captureAuth?.Invoke(request);
            return Task.FromResult(this.factory(request));
        }
    }
}