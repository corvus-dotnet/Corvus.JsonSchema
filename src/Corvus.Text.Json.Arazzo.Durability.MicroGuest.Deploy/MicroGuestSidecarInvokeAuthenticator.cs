// <copyright file="MicroGuestSidecarInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;

/// <summary>
/// Authenticates the runner's invocation of a micro-guest sandbox (ADR 0063, P1-10): the sidecar's admin surface takes
/// one shared bearer token, read from the runner's own secret store by reference and held for a bounded window, and the
/// invoke URL must be on the loopback interface, since the admin surface is the runner's own machine's and the token is
/// never sent anywhere else.
/// </summary>
public sealed class MicroGuestSidecarInvokeAuthenticator : IServerlessInvokeAuthenticator
{
    private readonly ISecretResolver secrets;
    private readonly SecretRef adminToken;
    private readonly TimeProvider timeProvider;
    private CachedToken? cached;

    /// <summary>Initializes a new instance of the <see cref="MicroGuestSidecarInvokeAuthenticator"/> class.</summary>
    /// <param name="secrets">The runner's secret resolver.</param>
    /// <param name="adminToken">The reference to the sidecar's admin token.</param>
    /// <param name="timeProvider">The clock the cache window is measured on, or <see langword="null"/> for the system clock.</param>
    public MicroGuestSidecarInvokeAuthenticator(ISecretResolver secrets, SecretRef adminToken, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        this.secrets = secrets;
        this.adminToken = adminToken;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets how long a resolved token is held before it is resolved again. Defaults to five minutes.</summary>
    public TimeSpan CacheWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is not { IsAbsoluteUri: true, IsLoopback: true })
        {
            throw new InvalidOperationException($"The sidecar's admin token is not sent to '{request.RequestUri}': a micro-guest invoke goes to the sidecar's loopback admin surface, and nowhere else.");
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        CachedToken? current = this.cached;
        if (current is null || now >= current.Expires)
        {
            using SecretMaterial material = await this.secrets.ResolveAsync(this.adminToken, cancellationToken).ConfigureAwait(false);
            current = new CachedToken(material.Reveal(), now + this.CacheWindow);
            this.cached = current;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.Value);
    }

    private sealed record CachedToken(string Value, DateTimeOffset Expires);
}