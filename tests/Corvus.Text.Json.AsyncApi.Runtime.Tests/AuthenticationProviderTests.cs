// <copyright file="AuthenticationProviderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for the concrete <see cref="IMessageAuthenticationProvider"/> implementations.
/// </summary>
[TestClass]
public class AuthenticationProviderTests
{
    [TestMethod]
    public async Task UserPasswordProvider_SetsUsernameAndPassword()
    {
        UserPasswordAuthenticationProvider provider = new("alice", "secret123");
        MessageAuthenticationContext context = new(SecuritySchemeType.UserPassword, "userPass");

        await provider.AuthenticateAsync(context);

        Assert.AreEqual("alice", context.Credentials["username"]);
        Assert.AreEqual("secret123", context.Credentials["password"]);
    }

    [TestMethod]
    public async Task ApiKeyProvider_SetsKey()
    {
        ApiKeyAuthenticationProvider provider = new("my-api-key-42");
        MessageAuthenticationContext context = new(SecuritySchemeType.ApiKey, "apiKey");

        await provider.AuthenticateAsync(context);

        Assert.AreEqual("my-api-key-42", context.Credentials["key"]);
        Assert.IsFalse(context.Credentials.ContainsKey("name"));
        Assert.IsFalse(context.Credentials.ContainsKey("in"));
    }

    [TestMethod]
    public async Task ApiKeyProvider_SetsNameAndLocation_ForHttpApiKey()
    {
        ApiKeyAuthenticationProvider provider = new("key-value", name: "X-API-Key", location: "header");
        MessageAuthenticationContext context = new(SecuritySchemeType.HttpApiKey, "httpApiKey");

        await provider.AuthenticateAsync(context);

        Assert.AreEqual("key-value", context.Credentials["key"]);
        Assert.AreEqual("X-API-Key", context.Credentials["name"]);
        Assert.AreEqual("header", context.Credentials["in"]);
    }

    [TestMethod]
    public async Task BearerTokenProvider_StaticToken_SetsToken()
    {
        BearerTokenAuthenticationProvider provider = new("eyJhbGciOiJIUzI1NiJ9.test");
        MessageAuthenticationContext context = new(SecuritySchemeType.Http, "bearer");

        await provider.AuthenticateAsync(context);

        Assert.AreEqual("eyJhbGciOiJIUzI1NiJ9.test", context.Credentials["token"]);
    }

    [TestMethod]
    public async Task BearerTokenProvider_DynamicFactory_AcquiresToken()
    {
        int callCount = 0;
        BearerTokenAuthenticationProvider provider = new(ct =>
        {
            callCount++;
            return new ValueTask<string>($"token-{callCount}");
        });

        MessageAuthenticationContext context1 = new(SecuritySchemeType.Http, "bearer");
        await provider.AuthenticateAsync(context1);
        Assert.AreEqual("token-1", context1.Credentials["token"]);

        MessageAuthenticationContext context2 = new(SecuritySchemeType.Http, "bearer");
        await provider.AuthenticateAsync(context2);
        Assert.AreEqual("token-2", context2.Credentials["token"]);
    }

    [TestMethod]
    public async Task CertificateProvider_SetsBase64Certificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest req = new("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        byte[] pfxBytes = req
            .CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1))
            .Export(X509ContentType.Pfx);
        using X509Certificate2 cert = X509CertificateLoader.LoadPkcs12(
            pfxBytes, null, X509KeyStorageFlags.Exportable);

        CertificateAuthenticationProvider provider = new(cert);
        MessageAuthenticationContext context = new(SecuritySchemeType.X509, "clientCert");

        await provider.AuthenticateAsync(context);

        Assert.IsTrue(context.Credentials.ContainsKey("certificate"));
        byte[] decoded = Convert.FromBase64String(context.Credentials["certificate"]);
        Assert.IsTrue(decoded.Length > 0);
    }

    [TestMethod]
    public async Task OAuth2Provider_StaticToken_SetsAccessToken()
    {
        OAuth2AuthenticationProvider provider = new("access-token-xyz", scopes: "read write");
        MessageAuthenticationContext context = new(SecuritySchemeType.OAuth2, "oauth2");

        await provider.AuthenticateAsync(context);

        Assert.AreEqual("access-token-xyz", context.Credentials["access_token"]);
        Assert.AreEqual("Bearer", context.Credentials["token_type"]);
        Assert.AreEqual("read write", context.Credentials["scopes"]);
    }

    [TestMethod]
    public async Task OAuth2Provider_DynamicFactory_AcquiresToken()
    {
        int callCount = 0;
        OAuth2AuthenticationProvider provider = new(
            _ => new ValueTask<string>($"refreshed-{++callCount}"),
            tokenType: "MAC");

        MessageAuthenticationContext context = new(SecuritySchemeType.OAuth2, "oauth2");
        await provider.AuthenticateAsync(context);

        Assert.AreEqual("refreshed-1", context.Credentials["access_token"]);
        Assert.AreEqual("MAC", context.Credentials["token_type"]);
        Assert.IsFalse(context.Credentials.ContainsKey("scopes"));
    }

    [TestMethod]
    public async Task CompositeProvider_DelegatesToCorrectProvider()
    {
        UserPasswordAuthenticationProvider userPass = new("user", "pass");
        ApiKeyAuthenticationProvider apiKey = new("the-key");

        CompositeAuthenticationProvider composite = new(
        [
            new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>(
                SecuritySchemeType.UserPassword, userPass),
            new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>(
                SecuritySchemeType.ApiKey, apiKey),
        ]);

        // UserPassword context
        MessageAuthenticationContext ctx1 = new(SecuritySchemeType.UserPassword, "userPass");
        await composite.AuthenticateAsync(ctx1);
        Assert.AreEqual("user", ctx1.Credentials["username"]);
        Assert.IsFalse(ctx1.Credentials.ContainsKey("key"));

        // ApiKey context
        MessageAuthenticationContext ctx2 = new(SecuritySchemeType.ApiKey, "apiKey");
        await composite.AuthenticateAsync(ctx2);
        Assert.AreEqual("the-key", ctx2.Credentials["key"]);
        Assert.IsFalse(ctx2.Credentials.ContainsKey("username"));
    }

    [TestMethod]
    public async Task CompositeProvider_NoOp_ForUnknownSchemeType()
    {
        CompositeAuthenticationProvider composite = new(
        [
            new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>(
                SecuritySchemeType.UserPassword, new UserPasswordAuthenticationProvider("u", "p")),
        ]);

        MessageAuthenticationContext context = new(SecuritySchemeType.OAuth2, "unknown");
        await composite.AuthenticateAsync(context);

        Assert.AreEqual(0, context.Credentials.Count);
    }
}