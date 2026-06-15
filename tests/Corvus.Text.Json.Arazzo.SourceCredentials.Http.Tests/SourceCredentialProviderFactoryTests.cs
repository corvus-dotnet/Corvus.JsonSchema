// <copyright file="SourceCredentialProviderFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>Tests that <see cref="SourceCredentialProviderFactory"/> builds the right provider per auth kind, resolves
/// the correct reference, and scrubs the resolved material.</summary>
[TestClass]
public sealed class SourceCredentialProviderFactoryTests
{
    [TestMethod]
    public async Task ApiKey_builds_a_header_api_key_provider_and_scrubs_the_secret()
    {
        var resolver = new FakeSecretResolver(new() { ["env://PETSTORE_KEY"] = "secret-key" });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "petstore",
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", "env://PETSTORE_KEY")],
            [new CredentialConfigDefinition("parameterName", "X-Api-Key")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        string? header = await ApplyAndGetHeaderAsync(provider, "X-Api-Key");
        header.ShouldBe("secret-key");

        // The factory scrubbed the resolved material once the provider held the derived header.
        resolver.Issued.ShouldHaveSingleItem();
        Should.Throw<ObjectDisposedException>(() => resolver.Issued[0].Reveal());
    }

    [TestMethod]
    public async Task Bearer_builds_an_authorization_bearer_provider()
    {
        var resolver = new FakeSecretResolver(new() { ["env://TOKEN"] = "abc123" });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api",
            "production",
            SourceCredentialKind.Bearer,
            [new SecretReferenceDefinition("value", "env://TOKEN")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("abc123");
    }

    [TestMethod]
    public async Task Basic_builds_an_authorization_basic_provider_from_config_username_and_secret_password()
    {
        var resolver = new FakeSecretResolver(new() { ["env://PWD"] = "p@ss" });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api",
            "production",
            SourceCredentialKind.Basic,
            [new SecretReferenceDefinition("password", "env://PWD")],
            [new CredentialConfigDefinition("username", "alice")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        request.Headers.Authorization!.Scheme.ShouldBe("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)).ShouldBe("alice:p@ss");
    }

    [TestMethod]
    public async Task OAuth2_builds_a_provider_that_fetches_and_applies_a_bearer_token()
    {
        var resolver = new FakeSecretResolver(new() { ["env://CLIENT_SECRET"] = "shh" });
        var handler = new MockHttpHandler(HttpStatusCode.OK, "{\"access_token\":\"tok-1\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");
        using var tokenClient = new HttpClient(handler);
        var factory = new SourceCredentialProviderFactory(resolver, tokenClient);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api",
            "production",
            SourceCredentialKind.OAuth2ClientCredentials,
            [new SecretReferenceDefinition("clientSecret", "env://CLIENT_SECRET")],
            [new CredentialConfigDefinition("tokenUrl", "https://auth.example/token"), new CredentialConfigDefinition("clientId", "client-1"), new CredentialConfigDefinition("scopes", "read")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);

        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("tok-1");
        handler.RequestBodies.ShouldHaveSingleItem();
        handler.RequestBodies[0].ShouldContain("grant_type=client_credentials");
        handler.RequestBodies[0].ShouldContain("client_id=client-1");
        handler.RequestBodies[0].ShouldContain("scope=read");
        (provider as IDisposable)?.Dispose();
    }

    [TestMethod]
    public async Task A_missing_required_reference_or_config_throws()
    {
        var resolver = new FakeSecretResolver(new() { ["env://X"] = "x" });
        var factory = new SourceCredentialProviderFactory(resolver);

        // Basic without a username config.
        SourceCredentialBinding noUsername = BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.Basic, [new SecretReferenceDefinition("password", "env://X")]));
        await Should.ThrowAsync<InvalidOperationException>(async () => await factory.CreateAsync(noUsername, default));

        // OAuth2 with no token client supplied to the factory.
        SourceCredentialBinding oauth = BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.OAuth2ClientCredentials, [new SecretReferenceDefinition("clientSecret", "env://X")],
            [new CredentialConfigDefinition("tokenUrl", "https://auth.example/token"), new CredentialConfigDefinition("clientId", "c")]));
        await Should.ThrowAsync<InvalidOperationException>(async () => await factory.CreateAsync(oauth, default));
    }

    private static async Task<string?> ApplyAndGetHeaderAsync(IHttpAuthenticationProvider provider, string headerName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        return request.Headers.TryGetValues(headerName, out IEnumerable<string>? values) ? values.Single() : null;
    }
}