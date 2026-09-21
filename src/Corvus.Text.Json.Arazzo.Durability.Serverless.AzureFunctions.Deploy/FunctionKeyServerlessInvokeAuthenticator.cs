// <copyright file="FunctionKeyServerlessInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// Presents the invoke function key on the runner's invocation of a deployed Function App (ADR 0059 decision 4). The
/// baked trigger is at the <c>Function</c> authorization level, so the Functions host refuses an invocation without it.
/// The key is the secret <see cref="AzureFunctionsInvokeAuthorization.InvokeKey"/> names in the runner's secret store,
/// the same secret the deployer set on the app.
/// </summary>
/// <remarks>
/// The key is sent in the <c>x-functions-key</c> header and never in the query string, so it does not reach a request
/// log. It is refused over plain HTTP to anything but the loopback interface. The resolved value is held for
/// <see cref="CacheWindow"/> so that an advance does not cost a round trip to the secret store, which bounds how long a
/// rotated key goes unnoticed.
/// </remarks>
public sealed class FunctionKeyServerlessInvokeAuthenticator : IServerlessInvokeAuthenticator
{
    private const string KeyHeader = "x-functions-key";

    private readonly ISecretResolver secrets;
    private readonly SecretRef invokeKey;
    private readonly TimeProvider timeProvider;
    private CachedKey? cached;

    /// <summary>Initializes a new instance of the <see cref="FunctionKeyServerlessInvokeAuthenticator"/> class.</summary>
    /// <param name="secrets">The runner's secret resolver.</param>
    /// <param name="invokeKey">The reference to the invoke key.</param>
    /// <param name="timeProvider">The clock the cache window is measured on, or <see langword="null"/> for the system clock.</param>
    public FunctionKeyServerlessInvokeAuthenticator(ISecretResolver secrets, SecretRef invokeKey, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        this.secrets = secrets;
        this.invokeKey = invokeKey;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets how long a resolved key is held before it is resolved again. Defaults to five minutes.</summary>
    public TimeSpan CacheWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is not { IsAbsoluteUri: true } url
            || (!url.IsLoopback && !url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"The function key is not sent to '{request.RequestUri}': it goes over HTTPS, or to the loopback interface, and nowhere else.");
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        CachedKey? current = this.cached;
        if (current is null || now >= current.Expires)
        {
            using SecretMaterial material = await this.secrets.ResolveAsync(this.invokeKey, cancellationToken).ConfigureAwait(false);
            current = new CachedKey(material.Reveal(), now + this.CacheWindow);
            this.cached = current;
        }

        request.Headers.Remove(KeyHeader);
        request.Headers.TryAddWithoutValidation(KeyHeader, current.Value);
    }

    private sealed record CachedKey(string Value, DateTimeOffset Expires);
}