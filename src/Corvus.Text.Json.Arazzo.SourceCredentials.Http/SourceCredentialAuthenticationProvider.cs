// <copyright file="SourceCredentialAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// The stable, per-source <see cref="IHttpAuthenticationProvider"/> a runner attaches to a source's transport
/// (design §13): on every request it pulls the <em>current</em> built provider from the runner's
/// <see cref="SourceCredentialCache"/> for (<see cref="sourceName"/>, <see cref="environment"/>) and applies it. So
/// one provider instance, fixed at transport-bind time, transparently tracks rotation and TTL refresh — a rotated
/// secret (or a freshly-minted OAuth token) is picked up on the next request with no rebind, which is why a
/// <em>resumed</em> run automatically authenticates against the current credential (§13.3).
/// </summary>
/// <remarks>
/// This holds no secret and no resolver — only the cache and the source key. A source with no binding resolves to
/// <see langword="null"/> and the request is left unauthenticated (no <c>Authorization</c> header), never an error.
/// The warm cache read is allocation-free (§13.4), so the per-request overhead is a dictionary lookup plus the
/// underlying provider's pre-built-header apply.
/// </remarks>
public sealed class SourceCredentialAuthenticationProvider : IHttpAuthenticationProvider
{
    private readonly SourceCredentialCache cache;
    private readonly string sourceName;
    private readonly string environment;

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialAuthenticationProvider"/> class.</summary>
    /// <param name="cache">The runner credential cache the current provider is read from.</param>
    /// <param name="sourceName">The Arazzo source description name this transport authenticates calls to.</param>
    /// <param name="environment">The deployment environment.</param>
    public SourceCredentialAuthenticationProvider(SourceCredentialCache cache, string sourceName, string environment)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        this.cache = cache;
        this.sourceName = sourceName;
        this.environment = environment;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        IHttpAuthenticationProvider? provider = await this.cache.GetAsync(this.sourceName, this.environment, cancellationToken).ConfigureAwait(false);
        if (provider is not null)
        {
            await provider.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}