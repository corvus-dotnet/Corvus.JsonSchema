// <copyright file="SourceCredentialProviderFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    public async Task OAuth2_rejects_a_non_https_token_endpoint_by_default()
    {
        // §17.5/F5: a cleartext token endpoint would expose the POSTed client secret on the wire.
        var resolver = new FakeSecretResolver(new() { ["env://CLIENT_SECRET"] = "shh" });
        using var tokenClient = new HttpClient(new MockHttpHandler(HttpStatusCode.OK, "{}"));
        var factory = new SourceCredentialProviderFactory(resolver, tokenClient);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api",
            "production",
            SourceCredentialKind.OAuth2ClientCredentials,
            [new SecretReferenceDefinition("clientSecret", "env://CLIENT_SECRET")],
            [new CredentialConfigDefinition("tokenUrl", "http://auth.example/token"), new CredentialConfigDefinition("clientId", "client-1")]));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () => await factory.CreateAsync(binding, default));
        ex.Message.ShouldContain("non-https");

        // The client secret was never resolved (the guard fires before secret resolution).
        resolver.Issued.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task OAuth2_allows_a_non_https_token_endpoint_when_the_deployment_opts_in()
    {
        var resolver = new FakeSecretResolver(new() { ["env://CLIENT_SECRET"] = "shh" });
        var handler = new MockHttpHandler(HttpStatusCode.OK, "{\"access_token\":\"tok-1\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");
        using var tokenClient = new HttpClient(handler);
        var factory = new SourceCredentialProviderFactory(resolver, tokenClient, allowInsecureOAuthTokenEndpoint: true);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api",
            "production",
            SourceCredentialKind.OAuth2ClientCredentials,
            [new SecretReferenceDefinition("clientSecret", "env://CLIENT_SECRET")],
            [new CredentialConfigDefinition("tokenUrl", "http://auth.example/token"), new CredentialConfigDefinition("clientId", "client-1")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        request.Headers.Authorization!.Parameter.ShouldBe("tok-1");
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

    [TestMethod]
    public async Task Mtls_resolves_a_client_certificate_from_a_base64_pfx()
    {
        using X509Certificate2 source = SelfSigned("CN=mtls-pfx", out _);
        var resolver = new FakeSecretResolver(new() { ["env://CERT"] = Convert.ToBase64String(source.Export(X509ContentType.Pkcs12)) });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.Mtls, [new SecretReferenceDefinition("certificate", "env://CERT")]));

        using X509Certificate2 resolved = await factory.ResolveClientCertificateAsync(binding, default);
        resolved.Subject.ShouldBe("CN=mtls-pfx");
        resolved.HasPrivateKey.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Mtls_resolves_a_passphrase_protected_pfx()
    {
        using X509Certificate2 source = SelfSigned("CN=mtls-pfx-pass", out _);
        var resolver = new FakeSecretResolver(new() { ["env://CERT"] = Convert.ToBase64String(source.Export(X509ContentType.Pkcs12, "p@ss")), ["env://PASS"] = "p@ss" });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.Mtls,
            [new SecretReferenceDefinition("certificate", "env://CERT"), new SecretReferenceDefinition("passphrase", "env://PASS")]));

        using X509Certificate2 resolved = await factory.ResolveClientCertificateAsync(binding, default);
        resolved.Subject.ShouldBe("CN=mtls-pfx-pass");
        resolved.HasPrivateKey.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Mtls_resolves_a_pem_certificate_paired_with_a_pem_private_key()
    {
        using X509Certificate2 source = SelfSigned("CN=mtls-pem", out RSA key);
        using (key)
        {
            var resolver = new FakeSecretResolver(new() { ["env://CERT"] = source.ExportCertificatePem(), ["env://KEY"] = key.ExportPkcs8PrivateKeyPem() });
            var factory = new SourceCredentialProviderFactory(resolver);
            SourceCredentialBinding binding = BindingFactory.Create(new(
                "api", "production", SourceCredentialKind.Mtls,
                [new SecretReferenceDefinition("certificate", "env://CERT"), new SecretReferenceDefinition("privateKey", "env://KEY")]));

            using X509Certificate2 resolved = await factory.ResolveClientCertificateAsync(binding, default);
            resolved.Subject.ShouldBe("CN=mtls-pem");
            resolved.HasPrivateKey.ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task Mtls_resolves_an_encrypted_pem_private_key_with_a_passphrase()
    {
        using X509Certificate2 source = SelfSigned("CN=mtls-pem-enc", out RSA key);
        using (key)
        {
            string encryptedKeyPem = key.ExportEncryptedPkcs8PrivateKeyPem("keypwd", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
            var resolver = new FakeSecretResolver(new() { ["env://CERT"] = source.ExportCertificatePem(), ["env://KEY"] = encryptedKeyPem, ["env://PASS"] = "keypwd" });
            var factory = new SourceCredentialProviderFactory(resolver);
            SourceCredentialBinding binding = BindingFactory.Create(new(
                "api", "production", SourceCredentialKind.Mtls,
                [new SecretReferenceDefinition("certificate", "env://CERT"), new SecretReferenceDefinition("privateKey", "env://KEY"), new SecretReferenceDefinition("passphrase", "env://PASS")]));

            using X509Certificate2 resolved = await factory.ResolveClientCertificateAsync(binding, default);
            resolved.Subject.ShouldBe("CN=mtls-pem-enc");
            resolved.HasPrivateKey.ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task Mtls_per_request_provider_is_a_no_op()
    {
        // The certificate authenticates the connection at the TLS handshake; nothing is applied per request.
        using X509Certificate2 source = SelfSigned("CN=noop", out _);
        var resolver = new FakeSecretResolver(new() { ["env://CERT"] = Convert.ToBase64String(source.Export(X509ContentType.Pkcs12)) });
        var factory = new SourceCredentialProviderFactory(resolver);
        SourceCredentialBinding binding = BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.Mtls, [new SecretReferenceDefinition("certificate", "env://CERT")]));

        IHttpAuthenticationProvider provider = await factory.CreateAsync(binding, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        request.Headers.Authorization.ShouldBeNull();
    }

    [TestMethod]
    public void Mtls_without_a_certificate_reference_is_rejected_at_the_validation_boundary()
    {
        // An mTLS binding with no 'certificate' reference cannot be persisted: ValidateDraft (which the control plane
        // surfaces as a 400) rejects it before it ever reaches the runner's certificate resolution.
        ArgumentException ex = Should.Throw<ArgumentException>(() => BindingFactory.Create(new(
            "api", "production", SourceCredentialKind.Mtls, [new SecretReferenceDefinition("notTheCert", "env://X")])));
        ex.Message.ShouldContain("certificate");
    }

    // A short-lived self-signed certificate for the mTLS tests; `key` is handed back (for PEM export) but the caller owns it.
    private static X509Certificate2 SelfSigned(string subject, out RSA key)
    {
        key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static async Task<string?> ApplyAndGetHeaderAsync(IHttpAuthenticationProvider provider, string headerName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example/");
        await provider.AuthenticateAsync(request, default);
        return request.Headers.TryGetValues(headerName, out IEnumerable<string>? values) ? values.Single() : null;
    }
}