// <copyright file="EntraServerlessInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using Azure.Core;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// Layers Microsoft Entra authentication on top of the function key for the runner's invocation of a deployed Function
/// App (ADR 0059 decision 4). It presents the key through the authenticator it wraps, then adds a bearer token issued to
/// the runner's identity for the app's audience, which the app's built-in authentication validates before the Functions
/// host sees the request.
/// </summary>
/// <remarks>
/// The key authenticator is required, not optional: Entra is a second layer and never the only one, so switching the
/// app's authentication off leaves the function behind its key and not open. The credential is the runner's, supplied
/// for the environment (a managed identity or a workload identity), and it caches and renews its own tokens.
/// </remarks>
public sealed class EntraServerlessInvokeAuthenticator : IServerlessInvokeAuthenticator
{
    private readonly FunctionKeyServerlessInvokeAuthenticator functionKey;
    private readonly TokenCredential credential;
    private readonly TokenRequestContext tokenRequest;

    /// <summary>Initializes a new instance of the <see cref="EntraServerlessInvokeAuthenticator"/> class.</summary>
    /// <param name="functionKey">The function-key authenticator this layers on.</param>
    /// <param name="credential">The runner's credential for the environment.</param>
    /// <param name="audience">The Function App's Entra audience, as in <see cref="AzureFunctionsInvokeAuthorization.EntraAudience"/>.</param>
    public EntraServerlessInvokeAuthenticator(FunctionKeyServerlessInvokeAuthenticator functionKey, TokenCredential credential, string audience)
    {
        ArgumentNullException.ThrowIfNull(functionKey);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrEmpty(audience);
        this.functionKey = functionKey;
        this.credential = credential;
        this.tokenRequest = new TokenRequestContext([audience.TrimEnd('/') + "/.default"]);
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        // The key goes on first: it also refuses a URL the token must not be sent to.
        await this.functionKey.AuthenticateAsync(request, body, cancellationToken).ConfigureAwait(false);
        AccessToken token = await this.credential.GetTokenAsync(this.tokenRequest, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}