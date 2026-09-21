// <copyright file="EntraServerlessInvokeAuthenticatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm.Tests;

/// <summary>
/// Proves <see cref="EntraServerlessInvokeAuthenticator"/> is a layer on the function key and never a replacement for
/// it: an invocation carries both the key and a bearer token issued for the app's audience.
/// </summary>
[TestClass]
public sealed class EntraServerlessInvokeAuthenticatorTests
{
    [TestMethod]
    public async Task An_invocation_carries_the_key_and_a_token_for_the_apps_audience()
    {
        var credential = new RecordingCredential("entra-token");
        var authenticator = new EntraServerlessInvokeAuthenticator(Key(), credential, "api://arazzo-functions/");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.azurewebsites.net/api/invoke");

        await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.GetValues("x-functions-key").Single().ShouldBe("k-1");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("entra-token");
        credential.Scopes.ShouldBe(["api://arazzo-functions/.default"]);
    }

    [TestMethod]
    public async Task No_token_is_requested_for_a_url_the_key_refuses()
    {
        var credential = new RecordingCredential("entra-token");
        var authenticator = new EntraServerlessInvokeAuthenticator(Key(), credential, "api://arazzo-functions");
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://app.azurewebsites.net/api/invoke");

        await Should.ThrowAsync<InvalidOperationException>(async () => await authenticator.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default));

        credential.Scopes.ShouldBeNull();
        request.Headers.Authorization.ShouldBeNull();
    }

    [TestMethod]
    public void The_key_layer_is_required()
    {
        Should.Throw<ArgumentNullException>(() => new EntraServerlessInvokeAuthenticator(null!, new RecordingCredential("t"), "api://a"));
        Should.Throw<ArgumentNullException>(() => new EntraServerlessInvokeAuthenticator(Key(), null!, "api://a"));
        Should.Throw<ArgumentException>(() => new EntraServerlessInvokeAuthenticator(Key(), new RecordingCredential("t"), string.Empty));
    }

    private static FunctionKeyServerlessInvokeAuthenticator Key()
        => new(new FixedResolver("k-1"), SecretRef.Parse("env://ARAZZO_INVOKE_KEY"));

    private sealed class FixedResolver(string value) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => ValueTask.FromResult(SecretMaterial.FromString(value));
    }

    private sealed class RecordingCredential(string token) : TokenCredential
    {
        public string[]? Scopes { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            this.Scopes = requestContext.Scopes;
            return new AccessToken(token, DateTimeOffset.MaxValue);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(this.GetToken(requestContext, cancellationToken));
    }
}